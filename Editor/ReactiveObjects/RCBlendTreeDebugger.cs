#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    /// <summary>
    /// Editor window that dumps the MA/RC Apply blend tree for the selected avatar,
    /// annotated with current animator parameter values and active-branch markers.
    /// Active branches are green and expanded; inactive branches are grey and collapsed.
    /// Open via GameObject → Modular Avatar → Dump RC Blend Tree.
    /// </summary>
    internal class RCBlendTreeDebugger : EditorWindow
    {
        private DumpNode? _root;
        private string[] _paramLines = Array.Empty<string>();
        private readonly Dictionary<string, bool> _foldouts = new();
        private Vector2 _scroll;
        private GameObject? _target;

        private GUIStyle? _foldoutStyle;
        private GUIStyle? _labelStyle;
        private GUIStyle? _curveStyle;

        private string _searchText = "";
        private string _lastAppliedSearch = "";

        private const string ActiveColor = "#00E676";
        private const string InactiveColor = "#888888";

        [MenuItem(UnityMenuItems.GameObject_DumpRCBlendTree,
            false, UnityMenuItems.GameObject_DumpRCBlendTreeOrder)]
        private static void Open(MenuCommand cmd)
        {
            var win = GetWindow<RCBlendTreeDebugger>("RC Blend Tree");
            win._target = (cmd.context as GameObject) ?? Selection.activeGameObject;
            win.Refresh();
        }

        private void OnGUI()
        {
            EnsureStyles();

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    _target = Selection.activeGameObject;
                    Refresh();
                }
                _target = EditorGUILayout.ObjectField(_target, typeof(GameObject), true) as GameObject;
            }

            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search", GUILayout.Width(46));
                var newSearch = GUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
                if (GUILayout.Button(GUIContent.none, new GUIStyle("ToolbarSearchCancelButton")))
                    newSearch = "";
                if (newSearch != _searchText)
                {
                    _searchText = newSearch;
                    if (_root != null)
                    {
                        _foldouts.Clear();
                        if (!string.IsNullOrEmpty(_searchText))
                            SubtreeSearch(_root, "r", _searchText);
                        _lastAppliedSearch = _searchText;
                    }
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var line in _paramLines)
                EditorGUILayout.LabelField(line, _labelStyle);

            if (_root != null)
            {
                EditorGUILayout.Space(4);
                DrawNode(_root, "r");
            }

            EditorGUILayout.EndScrollView();
        }

        private void EnsureStyles()
        {
            if (_foldoutStyle != null) return;
            var mono = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
            _foldoutStyle = new GUIStyle(EditorStyles.foldout)
                { richText = true, font = mono };
            _labelStyle = new GUIStyle(EditorStyles.label)
                { richText = true, wordWrap = false, font = mono };
            _curveStyle = new GUIStyle(EditorStyles.miniLabel)
                { richText = true, wordWrap = false, font = mono };
        }

        // ── recursive IMGUI renderer ──────────────────────────────────────────

        private void DrawNode(DumpNode node, string path)
        {
            var label = Colorize(node.Header, node.Active);

            if (node is ClipDumpNode clip)
            {
                if (clip.CurveLines.Count == 0)
                {
                    EditorGUILayout.LabelField(label, _labelStyle);
                    return;
                }

                var open = GetOrInit(path, node.Active != false);
                open = EditorGUILayout.Foldout(open, label, true, _foldoutStyle);
                _foldouts[path] = open;
                if (open)
                {
                    EditorGUI.indentLevel++;
                    foreach (var line in clip.CurveLines)
                        EditorGUILayout.LabelField(Colorize(line, node.Active), _curveStyle);
                    EditorGUI.indentLevel--;
                }

                return;
            }

            if (node is BTreeDumpNode bt)
            {
                if (bt.Children.Count == 0)
                {
                    EditorGUILayout.LabelField(label, _labelStyle);
                    return;
                }

                var open = GetOrInit(path, node.Active != false);
                open = EditorGUILayout.Foldout(open, label, true, _foldoutStyle);
                _foldouts[path] = open;
                if (open)
                {
                    EditorGUI.indentLevel++;
                    for (var i = 0; i < bt.Children.Count; i++)
                        DrawNode(bt.Children[i], $"{path}/{i}");
                    EditorGUI.indentLevel--;
                }
            }
        }

        private bool GetOrInit(string path, bool defaultOpen)
        {
            if (!_foldouts.TryGetValue(path, out var open))
                open = defaultOpen;
            return open;
        }

        private static string Colorize(string text, bool? active)
        {
            return active switch
            {
                true => $"<color={ActiveColor}>{text}</color>",
                false => $"<color={InactiveColor}>{text}</color>",
                null => text
            };
        }

        private void Refresh()
        {
            _foldouts.Clear();
            (_root, _paramLines) = RCBlendTreeDump.Build(_target);
            // Re-apply any active search so results reflect the newly-built tree.
            if (!string.IsNullOrEmpty(_searchText) && _root != null)
                SubtreeSearch(_root, "r", _searchText);
            _lastAppliedSearch = _searchText;
            Repaint();
        }

        // ── search ────────────────────────────────────────────────────────────

        /// <summary>
        /// Recursively marks foldouts open for every node that contains <paramref name="query"/>
        /// or has a descendant that does.  Nodes with no match in their subtree are marked closed.
        /// Returns true if this node or any descendant matched.
        /// </summary>
        private bool SubtreeSearch(DumpNode node, string path, string query)
        {
            var selfMatches = NodeContains(node, query);
            var childMatches = false;

            if (node is BTreeDumpNode bt)
                for (var i = 0; i < bt.Children.Count; i++)
                    if (SubtreeSearch(bt.Children[i], $"{path}/{i}", query))
                        childMatches = true;

            _foldouts[path] = selfMatches || childMatches;
            return selfMatches || childMatches;
        }

        private static bool NodeContains(DumpNode node, string query)
        {
            if (node.Header.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
            if (node is ClipDumpNode clip)
                foreach (var line in clip.CurveLines)
                    if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }
    }

    // ── dump node types ───────────────────────────────────────────────────────

    internal abstract class DumpNode
    {
        internal string Header = "";

        /// <summary>null = neutral (no colour), true = active path, false = inactive path.</summary>
        internal bool? Active;
    }

    internal class BTreeDumpNode : DumpNode
    {
        internal readonly List<DumpNode> Children = new();
    }

    internal class ClipDumpNode : DumpNode
    {
        internal readonly List<string> CurveLines = new();
    }

    // ── tree builder ──────────────────────────────────────────────────────────

    internal static class RCBlendTreeDump
    {
        internal static (DumpNode? root, string[] paramLines) Build(GameObject? root)
        {
            if (root == null) return (null, new[] { "(no selection)" });

            var animator = root.GetComponent<Animator>();
            if (animator == null) return (null, new[] { "No Animator component on the selected object." });

            AnimatorController? controller = null;
            // Default: read directly from the Animator's parameter store.
            Func<string, float> getParam = name => SafeGetFloat(animator, name);

#if MA_VRCSDK3_AVATARS
            // LyumaAv3Emulator drives the Animator through a PlayableGraph rather than
            // runtimeAnimatorController, so animator.GetFloat() returns stale defaults.
            // Detect it via reflection (optional dependency) and use its own parameter store.
            var emulatorRuntime = FindEmulatorRuntime(root);
            if (emulatorRuntime != null)
            {
                controller = GetEmulatorFxController(emulatorRuntime);
                getParam = name => ReadEmulatorFloat(emulatorRuntime, name);
            }

            if (controller == null)
            {
                var avDesc = root.GetComponent<VRCAvatarDescriptor>();
                if (avDesc != null)
                {
                    var fxLayer = Array.Find(avDesc.baseAnimationLayers,
                        l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
                    controller = fxLayer.animatorController as AnimatorController;
                }
            }
#endif
            if (controller == null)
            {
                var rc = animator.runtimeAnimatorController;
                while (rc is AnimatorOverrideController aoc) rc = aoc.runtimeAnimatorController;
                controller = rc as AnimatorController;
            }
            if (controller == null)
                return (null, new[] { "No AnimatorController found — build (bake) the avatar first." });

            AnimatorControllerLayer? applyLayer = null;
            foreach (var layer in controller.layers)
                if (layer.name == BakeContext.APPLY_LAYER_NAME) { applyLayer = layer; break; }
            if (applyLayer == null)
                return (null, new[] { $"No '{BakeContext.APPLY_LAYER_NAME}' layer found in the controller." });

            var paramLines = BuildParamLines(controller, getParam);

            var motion = applyLayer.stateMachine?.defaultState?.motion;
            if (motion == null)
                return (null, Append(paramLines, "(default state has no motion)"));

            var tree = new Builder(getParam).Visit(motion, null);
            return (tree, paramLines);
        }

        private static string[] BuildParamLines(AnimatorController controller, Func<string, float> getParam)
        {
            var lines = new List<string> { $"=== {BakeContext.APPLY_LAYER_NAME} ===", "RC parameters:" };
            bool any = false;
            foreach (var p in controller.parameters)
            {
                if (!p.name.StartsWith("$$MA/RC/")) continue;
                var val = getParam(p.name);
                lines.Add($"  {p.name,-55} = {val,12:G9}  {FloatBinary(val)}");
                any = true;
            }

            if (!any) lines.Add("  (none)");
            lines.Add("");
            return lines.ToArray();
        }

        private static string[] Append(string[] lines, string extra)
        {
            var result = new string[lines.Length + 1];
            lines.CopyTo(result, 0);
            result[lines.Length] = extra;
            return result;
        }

        // ── builder ───────────────────────────────────────────────────────────

        private sealed class Builder
        {
            private readonly Func<string, float> _getParam;

            internal Builder(Func<string, float> getParam)
            {
                _getParam = getParam;
            }

            internal DumpNode Visit(Motion? motion, bool? active)
            {
                return motion switch
                {
                    BlendTree bt => VisitBTree(bt, active),
                    AnimationClip c => VisitClip(c, active),
                    null => new BTreeDumpNode { Header = "(null)", Active = active },
                    _ => new BTreeDumpNode { Header = $"{motion.GetType().Name} \"{motion.name}\"", Active = active }
                };
            }

            private DumpNode VisitBTree(BlendTree bt, bool? active)
            {
                var node = new BTreeDumpNode { Header = BTreeHeader(bt), Active = active };
                var children = bt.children;

                if (bt.blendType == BlendTreeType.Simple1D)
                {
                    var (aLo, aHi) = FindActive1D(children, _getParam(bt.blendParameter));
                    var showBinary = bt.name == "Execution";
                    var i = 0;
                    while (i < children.Length)
                    {
                        // Collapse consecutive children that share the same Motion reference
                        // (PriorityNode execution trees emit two thresholds per logical branch
                        // to create a hard step; only one entry is needed).
                        var isActive = i == aLo || i == aHi;
                        var j = i + 1;
                        while (j < children.Length && ReferenceEquals(children[j].motion, children[i].motion))
                        {
                            if (j == aLo || j == aHi) isActive = true;
                            j++;
                        }

                        var bin = showBinary ? $"  {FloatBinary(children[i].threshold)}" : "";
                        var mark = isActive ? " ★" : "  ";
                        var tag = $"[{children[i].threshold:G9}{bin}{mark}] ";

                        var child = Visit(children[i].motion, isActive ? (bool?)true : false);
                        child.Header = tag + child.Header;
                        node.Children.Add(child);
                        i = j;
                    }
                }
                else
                {
                    foreach (var ch in children)
                    {
                        var param = string.IsNullOrEmpty(ch.directBlendParameter) ? "1" : ch.directBlendParameter;
                        var weight = string.IsNullOrEmpty(ch.directBlendParameter)
                            ? 1f
                            : _getParam(ch.directBlendParameter);
                        var tag = $"[d:{param}={weight:F4}] ";
                        var child = Visit(ch.motion, active);
                        child.Header = tag + child.Header;
                        node.Children.Add(child);
                    }
                }

                return node;
            }

            private DumpNode VisitClip(AnimationClip clip, bool? active)
            {
                var node = new ClipDumpNode { Header = $"\"{clip.name}\"", Active = active };

                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    var val = curve != null && curve.length > 0 ? curve[0].value : 0f;

                    var isPriority = b.type == typeof(Animator) && b.propertyName.Contains("PriorityNode");
                    var valStr = isPriority ? $"{val,12:G9}  {FloatBinary(val)}" : $"{val:G9}";
                    var path = string.IsNullOrEmpty(b.path) ? "" : $"{b.path}: ";
                    node.CurveLines.Add($"{path}{b.type?.Name ?? "?"}.{b.propertyName} = {valStr}");
                }

                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                    var rv = keys is { Length: > 0 } && keys[0].value != null
                        ? keys[0].value.ToString()!
                        : "null";
                    var path = string.IsNullOrEmpty(b.path) ? "" : $"{b.path}: ";
                    node.CurveLines.Add($"{path}{b.type?.Name ?? "?"}.{b.propertyName} = (ref) {rv}");
                }

                return node;
            }

            private string BTreeHeader(BlendTree bt)
            {
                switch (bt.blendType)
                {
                    case BlendTreeType.Direct:
                        return $"[Direct] \"{bt.name}\"";
                    case BlendTreeType.Simple1D:
                        var val = _getParam(bt.blendParameter);
                        var bin = bt.name == "Execution" ? $"  {FloatBinary(val)}" : "";
                        return $"[1D \"{bt.blendParameter}\" = {val:G9}{bin}]  \"{bt.name}\"";
                    default:
                        return $"[{bt.blendType}] \"{bt.name}\"";
                }
            }

        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static (int lo, int hi) FindActive1D(ChildMotion[] children, float val)
        {
            var lo = 0;
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].threshold <= val) lo = i;
                else break;
            }

            return (lo, Math.Min(lo + 1, children.Length - 1));
        }

        /// <summary>
        /// IEEE 754 single-precision bit pattern: [S_EEEEEEEE_MMMMMMMMMMMMMMMMMMMMMMM].
        /// For Priority Node sums (always in [1,2)) the exponent is always 01111111 and
        /// the mantissa bits directly map to which conditions are active.
        /// </summary>
        internal static string FloatBinary(float val)
        {
            var u = BitConverter.ToUInt32(BitConverter.GetBytes(val), 0);
            int  sign = (int)(u >> 31);
            uint exp  = (u >> 23) & 0xFFu;
            uint mant = u & 0x7FFFFFu;
            var e = Convert.ToString(exp, 2).PadLeft(8, '0');
            var m = Convert.ToString(mant, 2).PadLeft(23, '0');
            return $"[{sign}_{e}_{m}]";
        }

        private static float SafeGetFloat(Animator anim, string name)
        {
            try
            {
                return anim.GetFloat(name);
            }
            catch { return 0f; }
        }

        // ── LyumaAv3Emulator integration (reflection-based, optional dependency) ──

        // Resolved once; null if the emulator package is not present.
        private static readonly Type? _lyumaRuntimeType = ResolveType("LyumaAv3Runtime");

        private static Type? ResolveType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                try
                {
                    var t = asm.GetType(name);
                    if (t != null) return t;
                }
                catch
                {
                    /* skip assemblies that can't be reflected */
                }

            return null;
        }

        private static Component? FindEmulatorRuntime(GameObject go)
        {
            return _lyumaRuntimeType != null ? go.GetComponent(_lyumaRuntimeType) : null;
        }

#if MA_VRCSDK3_AVATARS
        private static AnimatorController? GetEmulatorFxController(Component runtime)
        {
            // allControllers is public Dictionary<VRCAvatarDescriptor.AnimLayerType, RuntimeAnimatorController>
            var dict = runtime.GetType()
                    .GetField("allControllers", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(runtime)
                as Dictionary<VRCAvatarDescriptor.AnimLayerType, RuntimeAnimatorController>;
            if (dict == null) return null;
            dict.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var rc);
            return rc as AnimatorController;
        }
#endif

        /// <summary>
        ///     Reads a float parameter from LyumaAv3Runtime.Floats[FloatToIndex[name]].value.
        ///     This is populated from the per-playable parameter store each frame, including
        ///     parameters that are controlled by animator curves (e.g. PriorityNode sums).
        /// </summary>
        private static float ReadEmulatorFloat(Component runtime, string name)
        {
            var rtype = runtime.GetType();
            if (rtype.GetField("FloatToIndex", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(runtime) is not Dictionary<string, int> index) return 0f;
            if (rtype.GetField("Floats", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(runtime) is not IList floats) return 0f;
            if (!index.TryGetValue(name, out var i) || i >= floats.Count) return 0f;
            var param = floats[i];
            return param == null
                ? 0f
                : (float)(param.GetType()
                    .GetField("value", BindingFlags.Public | BindingFlags.Instance)
                    ?.GetValue(param) ?? 0f);
        }
    }
}
