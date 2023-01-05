using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    /**
     * Ensures that any blendshapes marked for syncing by BlendshapeSync propagate values in all animation clips.
     *
     * Note that we only look at the FX layer, as any other layer won't work properly with mirror reflections anyway.
     */
    internal class BlendshapeSyncAnimationProcessor
    {
        private BuildContext _context;
        private Dictionary<Motion, Motion> _motionCache;
        private Dictionary<SummaryBinding, List<SummaryBinding>> _bindingMappings;

        private struct SummaryBinding
        {
            private const string PREFIX = "blendShape.";
            public string path;
            public string propertyName;

            public SummaryBinding(string path, string blendShape)
            {
                this.path = path;
                this.propertyName = PREFIX + blendShape;
            }

            public static SummaryBinding FromEditorBinding(EditorCurveBinding binding)
            {
                if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(PREFIX))
                {
                    return new SummaryBinding();
                }

                return new SummaryBinding(binding.path, binding.propertyName.Substring(PREFIX.Length));
            }
        }

        public void OnPreprocessAvatar(GameObject avatar, BuildContext context)
        {
            _context = context;
            var animDb = _context.AnimationDatabase;

            var avatarDescriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            _bindingMappings = new Dictionary<SummaryBinding, List<SummaryBinding>>();
            _motionCache = new Dictionary<Motion, Motion>();

            var components = avatarDescriptor.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            if (components.Length == 0) return;

            var layers = avatarDescriptor.baseAnimationLayers;
            var fxIndex = -1;
            AnimatorController controller = null;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].type == VRCAvatarDescriptor.AnimLayerType.FX && !layers[i].isDefault)
                {
                    if (layers[i].animatorController is AnimatorController c && c != null)
                    {
                        fxIndex = i;
                        controller = c;
                        break;
                    }
                }
            }

            if (controller == null)
            {
                // Nothing to do, return
            }

            foreach (var component in components)
            {
                var targetObj = RuntimeUtil.RelativePath(avatarDescriptor.gameObject, component.gameObject);

                foreach (var binding in component.Bindings)
                {
                    var refObj = binding.ReferenceMesh.Get(component);
                    if (refObj == null) continue;
                    var refSmr = refObj.GetComponent<SkinnedMeshRenderer>();
                    if (refSmr == null) continue;

                    var refPath = RuntimeUtil.RelativePath(avatarDescriptor.gameObject, refObj);

                    var srcBinding = new SummaryBinding(refPath, binding.Blendshape);

                    if (!_bindingMappings.TryGetValue(srcBinding, out var dstBindings))
                    {
                        dstBindings = new List<SummaryBinding>();
                        _bindingMappings[srcBinding] = dstBindings;
                    }

                    var targetBlendshapeName = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                        ? binding.Blendshape
                        : binding.LocalBlendshape;

                    dstBindings.Add(new SummaryBinding(targetObj, targetBlendshapeName));
                }
            }

            // Walk and transform all clips
            animDb.ForeachClip(clip =>
            {
                if (clip.CurrentClip is AnimationClip anim)
                {
                    clip.CurrentClip = TransformMotion(anim);
                }
            });
        }

        Motion TransformMotion(Motion motion)
        {
            if (motion == null) return null;
            if (_motionCache.TryGetValue(motion, out var cached)) return cached;

            switch (motion)
            {
                case AnimationClip clip:
                {
                    motion = ProcessClip(clip);

                    break;
                }

                case BlendTree tree:
                {
                    bool anyChanged = false;
                    var children = tree.children;

                    for (int i = 0; i < children.Length; i++)
                    {
                        var newM = TransformMotion(children[i].motion);
                        if (newM != children[i].motion)
                        {
                            anyChanged = true;
                            children[i].motion = newM;
                        }
                    }

                    if (anyChanged)
                    {
                        var newTree = new BlendTree();
                        EditorUtility.CopySerialized(tree, newTree);
                        _context.SaveAsset(newTree);

                        newTree.children = children;
                        motion = newTree;
                    }

                    break;
                }
                default:
                    Debug.LogWarning($"Ignoring unsupported motion type {motion.GetType()}");
                    break;
            }

            _motionCache[motion] = motion;
            return motion;
        }

        AnimationClip ProcessClip(AnimationClip origClip)
        {
            var clip = origClip;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (var binding in bindings)
            {
                if (!_bindingMappings.TryGetValue(SummaryBinding.FromEditorBinding(binding), out var dstBindings))
                {
                    continue;
                }

                if (clip == origClip)
                {
                    clip = Object.Instantiate(clip);
                }

                foreach (var dst in dstBindings)
                {
                    clip.SetCurve(dst.path, typeof(SkinnedMeshRenderer), dst.propertyName,
                        AnimationUtility.GetEditorCurve(origClip, binding));
                }
            }

            return clip;
        }

        IEnumerable<AnimatorState> AllStates(AnimatorController controller)
        {
            HashSet<AnimatorStateMachine> visitedStateMachines = new HashSet<AnimatorStateMachine>();
            Queue<AnimatorStateMachine> stateMachines = new Queue<AnimatorStateMachine>();

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine != null)
                    stateMachines.Enqueue(layer.stateMachine);
            }

            while (stateMachines.Count > 0)
            {
                var next = stateMachines.Dequeue();
                if (visitedStateMachines.Contains(next)) continue;
                visitedStateMachines.Add(next);

                foreach (var state in next.states)
                {
                    yield return state.state;
                }

                foreach (var sm in next.stateMachines)
                {
                    stateMachines.Enqueue(sm.stateMachine);
                }
            }
        }
    }
}