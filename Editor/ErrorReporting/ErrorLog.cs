using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    internal class BuildReport
    {
        private const string Path = "Library/ModularAvatarBuildReport.json";

        internal static void Log(ErrorSeverity severity, string code, params object[] objects)
        {
            ErrorReport.ReportError(Localization.L, severity, code, objects);
        }

        internal static void LogFatal(string code, params object[] objects)
        {
            ErrorReport.ReportError(Localization.L, ErrorSeverity.Error, code, objects);
        }

        internal static void LogException(Exception e, string additionalStackTrace = "")
        {
            ErrorReport.ReportException(e, additionalStackTrace);
        }

        internal static T ReportingObject<T>(UnityEngine.Object obj, Func<T> action)
        {
            return ErrorReport.WithContextObject(obj, action);
        }

        internal static void ReportingObject(UnityEngine.Object obj, Action action)
        {
            ErrorReport.WithContextObject(obj, action);
        }

        [Obsolete("Use NDMF's ObjectRegistry instead")]
        public static void RemapPaths(string original, string cloned)
        {
        }
    }
}