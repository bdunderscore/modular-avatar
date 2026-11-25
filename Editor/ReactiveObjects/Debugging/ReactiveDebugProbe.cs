#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal abstract class ReactiveDebugProbe : IDisposable
    {
        private sealed class DisabledProbe : ReactiveDebugProbe
        {
            internal override void ConfigurePlatform(string platform) { }
            internal override void Offer(string stage, GameObject avatarRoot, AnimatorServicesContext animatorServices) { }
            internal override void Offer(string stage, ReactionGraph graph) { }
            internal override void Offer(string stage, IReadOnlyList<ReactionGraph> graphs) { }
            internal override void Offer(string stage, EffectGroup group, VirtualMotion? motion) { }
            public override void Dispose() { }
        }

        internal static readonly ReactiveDebugProbe None = new DisabledProbe();

        internal static ReactiveDebugProbe Create()
        {
            return ReactiveDebugDump.Enabled ? new EnabledProbe() : None;
        }

        internal abstract void Offer(string stage, GameObject avatarRoot, AnimatorServicesContext animatorServices);
        internal abstract void Offer(string stage, ReactionGraph graph);
        internal abstract void Offer(string stage, IReadOnlyList<ReactionGraph> graphs);

        internal abstract void ConfigurePlatform(string platform);
        internal abstract void Offer(string stage, EffectGroup group, VirtualMotion? motion);

        private sealed class EnabledProbe : ReactiveDebugProbe
        {
            private readonly ReactiveDebugJson _json = new();
            private readonly JObject _dump = new()
            {
                ["formatVersion"] = 2,
                ["stages"] = new JArray(),
                ["effectGroups"] = new JArray(),
                ["errors"] = new JArray(),
            };

            private bool _disposed;

            private int _nextMotionId = 1;

            internal override void ConfigurePlatform(string platform)
            {
                _json.Platform = platform;
            }

            internal override void Offer(string stage, GameObject avatarRoot, AnimatorServicesContext animatorServices)
            {
                Capture(stage, () => _dump["input"] = ReactiveSourceSnapshot.Capture(avatarRoot, animatorServices, _json,
                    (operation, exception) => RecordCaptureError($"{stage}: {operation}", exception)));
            }

            internal override void Offer(string stage, ReactionGraph graph)
            {
                Capture(stage, () => ((JArray)_dump["stages"]!).Add(new JObject
                {
                    ["name"] = stage,
                    ["graphs"] = new JArray(ReactiveDebugGraphSnapshot.SerializeGraph(graph, _json)),
                }));
            }

            internal override void Offer(string stage, IReadOnlyList<ReactionGraph> graphs)
            {
                Capture(stage, () =>
                {
                    var serializedGraphs = new JArray();
                    foreach (var graph in graphs)
                    {
                        serializedGraphs.Add(ReactiveDebugGraphSnapshot.SerializeGraph(graph, _json));
                    }

                    ((JArray)_dump["stages"]!).Add(new JObject
                    {
                        ["name"] = stage,
                        ["graphs"] = serializedGraphs,
                    });
                });
            }

            internal override void Offer(string stage, EffectGroup group, VirtualMotion? motion)
            {
                Capture(stage, () =>
                {
                    var record = ReactiveDebugGraphSnapshot.SerializeEffectGroup(group, motion, _json,
                        ref _nextMotionId);
                    ((JArray)_dump["effectGroups"]!).Add(record);
                });
            }

            private void Capture(string operation, Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    RecordCaptureError(operation, exception);
                    Debug.LogWarning($"Failed to capture reactive component debug data: {exception}");
                }
            }

            private void RecordCaptureError(string operation, Exception exception)
            {
                ((JArray)_dump["errors"]!).Add(new JObject
                {
                    ["operation"] = operation,
                    ["code"] = "CaptureFailed",
                    ["exceptionType"] = exception.GetType().Name,
                    // Exception messages may contain source component data. The Unity Console retains those details.
                    ["message"] = "Capture failed. See the Unity Console for details.",
                });
            }

            public override void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                try
                {
                    var projectDirectory = Path.GetDirectoryName(Application.dataPath);
                    if (projectDirectory == null) throw new InvalidOperationException("Unable to locate the Unity project directory");

                    var dumpDirectory = Path.Combine(projectDirectory, "Library", "nadena.dev.modular-avatar",
                        "ReactiveComponentDumps");
                    Directory.CreateDirectory(dumpDirectory);
                    var filename = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.json";
                    File.WriteAllText(Path.Combine(dumpDirectory, filename), _dump.ToString(Formatting.Indented));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to write reactive component debug dump: {exception}");
                }
            }
        }

        public abstract void Dispose();
    }
}
