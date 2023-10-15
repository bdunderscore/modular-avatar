using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    internal class AvatarReport
    {
        [JsonProperty] internal ObjectRef objectRef;

        [JsonProperty] internal bool successful;

        [JsonProperty] internal List<ErrorLog> logs = new List<ErrorLog>();
    }

    internal class ObjectRefLookupCache
    {
        private Dictionary<string, Dictionary<long, UnityEngine.Object>> _cache =
            new Dictionary<string, Dictionary<long, Object>>();

        internal UnityEngine.Object FindByGuidAndLocalId(string guid, long localId)
        {
            if (!_cache.TryGetValue(guid, out var fileContents))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                fileContents = new Dictionary<long, Object>(assets.Length);
                foreach (var asset in assets)
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var _, out long detectedId))
                    {
                        fileContents[detectedId] = asset;
                    }
                }

                _cache[guid] = fileContents;
            }

            if (fileContents.TryGetValue(localId, out var obj))
            {
                return obj;
            }
            else
            {
                return null;
            }
        }
    }

    internal struct ObjectRef
    {
        [JsonProperty] internal string guid;
        [JsonProperty] internal long? localId;
        [JsonProperty] internal string path, name;
        [JsonProperty] internal string typeName;

        internal ObjectRef(Object obj)
        {
            this.guid = null;
            localId = null;

            if (obj == null)
            {
                this.guid = path = name = null;
                localId = null;
                typeName = null;
                return;
            }

            typeName = obj.GetType().Name;

            long id;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out id))
            {
                this.guid = guid;
                localId = id;
            }

            if (obj is Component c)
            {
                path = RuntimeUtil.RelativePath(null, c.gameObject);
            }
            else if (obj is GameObject go)
            {
                path = RuntimeUtil.RelativePath(null, go);
            }
            else
            {
                path = null;
            }

            name = string.IsNullOrWhiteSpace(obj.name) ? "<???>" : obj.name;
        }

        internal UnityEngine.Object Lookup(ObjectRefLookupCache cache)
        {
            if (path != null)
            {
                return FindObject(path);
            }
            else if (guid != null && localId.HasValue)
            {
                return cache.FindByGuidAndLocalId(guid, localId.Value);
            }
            else
            {
                return null;
            }
        }

        private static GameObject FindObject(string path)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == path) return root;
                if (path.StartsWith(root.name + "/"))
                {
                    return root.transform.Find(path.Substring(root.name.Length + 1))?.gameObject;
                }
            }

            return null;
        }

        public ObjectRef Remap(string original, string cloned)
        {
            if (path == cloned)
            {
                path = original;
                name = path.Substring(path.LastIndexOf('/') + 1);
            }
            else if (path != null && path.StartsWith(cloned + "/"))
            {
                path = original + path.Substring(cloned.Length);
                name = path.Substring(path.LastIndexOf('/') + 1);
            }

            return this;
        }
    }

    internal enum ReportLevel
    {
        Validation,
        Info,
        Warning,
        Error,
        InternalError,
    }

    internal class ErrorLog
    {
        [JsonProperty] internal List<ObjectRef> referencedObjects;
        [JsonProperty] internal ReportLevel reportLevel;
        [JsonProperty] internal string messageCode;
        [JsonProperty] internal string[] substitutions;
        [JsonProperty] internal string stacktrace;

        internal ErrorLog(ReportLevel level, string code, string[] strings, params object[] args)
        {
            reportLevel = level;

            substitutions = strings.Select(s => s.ToString()).ToArray();

            referencedObjects = args.Where(o => o is Component || o is GameObject)
                .Select(o => new ObjectRef(o is Component c ? c.gameObject : (GameObject) o))
                .ToList();
            referencedObjects.AddRange(BuildReport.CurrentReport.GetActiveReferences());

            messageCode = code;
            stacktrace = null;
        }

        internal ErrorLog(ReportLevel level, string code, params object[] args) : this(level, code,
            Array.Empty<string>(), args)
        {
        }

        internal ErrorLog(Exception e, string additionalStackTrace = "")
        {
            reportLevel = ReportLevel.InternalError;
            messageCode = "error.internal_error";
            substitutions = new string[] {e.Message, e.TargetSite?.Name};
            referencedObjects = BuildReport.CurrentReport.GetActiveReferences().ToList();
            stacktrace = e.ToString() + additionalStackTrace;
        }

        public override string ToString()
        {
            return "[" + reportLevel + "] " + messageCode + " " + "subst: " + string.Join(", ", substitutions);
        }
    }

    internal class BuildReport
    {
        private const string Path = "Library/ModularAvatarBuildReport.json";

        private static BuildReport _report;
        private AvatarReport _currentAvatar;
        private Stack<UnityEngine.Object> _references = new Stack<Object>();

        [JsonProperty] internal List<AvatarReport> Avatars = new List<AvatarReport>();
        internal AvatarReport CurrentAvatar => _currentAvatar;

        public static BuildReport CurrentReport
        {
            get
            {
                if (_report == null) _report = LoadReport() ?? new BuildReport();
                return _report;
            }
        }

        static BuildReport()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                switch (change)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        // TODO - skip if we're doing a VRCSDK build
                        _report = new BuildReport();
                        break;
                }
            };
        }

        private static BuildReport LoadReport()
        {
            try
            {
                var data = File.ReadAllText(Path);
                return JsonConvert.DeserializeObject<BuildReport>(data);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void SaveReport()
        {
            var report = CurrentReport;
            var json = JsonConvert.SerializeObject(report);

            File.WriteAllText(Path, json);

            ErrorReportUI.reloadErrorReport();
        }

        private class AvatarReportScope : IDisposable
        {
            public void Dispose()
            {
                var successful = CurrentReport._currentAvatar.successful;
                CurrentReport._currentAvatar = null;
                BuildReport.SaveReport();
                if (!successful) throw new Exception("Avatar processing failed");
            }
        }

        internal IDisposable ReportingOnAvatar(GameObject avatarGameObject)
        {
            if (avatarGameObject != null)
            {
                AvatarReport report = new AvatarReport();
                report.objectRef = new ObjectRef(avatarGameObject);
                Avatars.Add(report);
                _currentAvatar = report;
                _currentAvatar.successful = true;

                _currentAvatar.logs.AddRange(ComponentValidation.ValidateAll(avatarGameObject));
            }

            return new AvatarReportScope();
        }

        internal static void Log(ReportLevel level, string code, object[] strings, params Object[] objects)
        {
            ErrorLog errorLog =
                new ErrorLog(level, code, strings: strings.Select(s => s.ToString()).ToArray(), objects);

            var avatarReport = CurrentReport._currentAvatar;
            if (avatarReport == null)
            {
                Debug.LogWarning("Error logged when not processing an avatar: " + errorLog);
                return;
            }

            avatarReport.logs.Add(errorLog);
        }

        internal static void LogFatal(string code, object[] strings, params Object[] objects)
        {
            Log(ReportLevel.Error, code, strings: strings, objects: objects);
            if (CurrentReport._currentAvatar != null)
            {
                CurrentReport._currentAvatar.successful = false;
            }
            else
            {
                throw new Exception("Fatal error without error reporting scope");
            }
        }

        internal static void LogException(Exception e, string additionalStackTrace = "")
        {
            var avatarReport = CurrentReport._currentAvatar;
            if (avatarReport == null)
            {
                Debug.LogException(e);
                return;
            }
            else
            {
                avatarReport.logs.Add(new ErrorLog(e, additionalStackTrace));
            }
        }

        internal static T ReportingObject<T>(UnityEngine.Object obj, Func<T> action)
        {
            if (obj != null) CurrentReport._references.Push(obj);
            try
            {
                return action();
            }
            catch (Exception e)
            {
                var additionalStackTrace = string.Join("\n", Environment.StackTrace.Split('\n').Skip(1)) + "\n";
                LogException(e, additionalStackTrace);
                return default;
            }
            finally
            {
                if (obj != null) CurrentReport._references.Pop();
            }
        }

        internal static void ReportingObject(UnityEngine.Object obj, Action action)
        {
            ReportingObject(obj, () =>
            {
                action();
                return true;
            });
        }

        internal IEnumerable<ObjectRef> GetActiveReferences()
        {
            return _references.Select(o => new ObjectRef(o));
        }

        public static void Clear()
        {
            _report = new BuildReport();
        }

        public static void RemapPaths(string original, string cloned)
        {
            foreach (var av in CurrentReport.Avatars)
            {
                av.objectRef = av.objectRef.Remap(original, cloned);

                foreach (var log in av.logs)
                {
                    log.referencedObjects = log.referencedObjects.Select(o => o.Remap(original, cloned)).ToList();
                }
            }

            ErrorReportUI.reloadErrorReport();
        }
    }
}