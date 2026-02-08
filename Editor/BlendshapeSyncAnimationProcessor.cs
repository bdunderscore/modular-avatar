#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal struct BlendshapeSyncSummaryBinding : IEquatable<BlendshapeSyncSummaryBinding>
    {
        private const string PREFIX = "blendShape.";
        public readonly SkinnedMeshRenderer Renderer;
        public string propertyName => PREFIX + BlendshapeName;
        public readonly string BlendshapeName;

        public BlendshapeSyncSummaryBinding(SkinnedMeshRenderer renderer, string blendShape)
        {
            Renderer = renderer;
            BlendshapeName = blendShape;
        }

        public static BlendshapeSyncSummaryBinding? FromEditorBinding(AnimatorServicesContext asc, EditorCurveBinding binding)
        {
            if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(PREFIX))
            {
                return null;
            }

            var obj = asc.ObjectPathRemapper.GetObjectForPath(binding.path);
            if (obj == null || !obj.TryGetComponent<SkinnedMeshRenderer>(out var smr)) return null;

            return new BlendshapeSyncSummaryBinding(smr, binding.propertyName.Substring(PREFIX.Length));
        }

        public EditorCurveBinding ToEditorCurveBinding(AnimatorServicesContext asc)
        {
            return EditorCurveBinding.FloatCurve(
                asc.ObjectPathRemapper.GetVirtualPathForObject(Renderer.gameObject),
                typeof(SkinnedMeshRenderer),
                propertyName
            );
        }

        public IEnumerable<EditorCurveBinding> ToSourceEditorCurveBindings(AnimatorServicesContext asc)
        {
            foreach (var path in asc.ObjectPathRemapper.GetAllPathsForObject(Renderer.gameObject))
            {
                yield return EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), propertyName);
            }
        }

        public bool Equals(BlendshapeSyncSummaryBinding other)
        {
            return Renderer == other.Renderer && propertyName == other.propertyName;
        }

        public override bool Equals(object? obj)
        {
            return obj is BlendshapeSyncSummaryBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Renderer, propertyName);
        }
    }

    internal class BlendshapeSyncAnimationProcessorState
    {
        public Dictionary<BlendshapeSyncSummaryBinding, List<BlendshapeSyncSummaryBinding>> BindingMappings = new Dictionary<BlendshapeSyncSummaryBinding, List<BlendshapeSyncSummaryBinding>>();
    }

    /**
     * Ensures that any blendshapes marked for syncing by BlendshapeSync propagate values in all animation clips.
     *
     * Note that we only look at the FX layer, as any other layer won't work properly with mirror reflections anyway.
     */
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class BlendshapeSyncAnimationProcessor : Pass<BlendshapeSyncAnimationProcessor>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var state = context.GetState<BlendshapeSyncAnimationProcessorState>();
            OnPreprocessAvatar(context, state);
        }

        private void OnPreprocessAvatar(ndmf.BuildContext context, BlendshapeSyncAnimationProcessorState state)
        {
            var avatarGameObject = context.AvatarRootObject;
            var asc = context.Extension<AnimatorServicesContext>();
            var animDb = asc.AnimationIndex;
            
            state.BindingMappings = new Dictionary<BlendshapeSyncSummaryBinding, List<BlendshapeSyncSummaryBinding>>();

            var components = avatarGameObject.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            if (components.Length == 0) return;

            foreach (var component in components)
            {
                BuildReport.ReportingObject(component, () => ProcessComponent(avatarGameObject, component, state));
            }

            // Apply the initial state of each binding to its targets
            foreach (var (source, targets) in state.BindingMappings)
            {
                var smr = source.Renderer;
                if (smr == null) continue;

                var srcIndex = smr.sharedMesh.GetBlendShapeIndex(source.BlendshapeName);
                if (srcIndex < 0) continue;

                var srcWeight = smr.GetBlendShapeWeight(srcIndex);

                foreach (var target in targets)
                {
                    var targetSmr = target.Renderer;
                    if (targetSmr == null) continue;

                    var targetIndex = targetSmr.sharedMesh.GetBlendShapeIndex(target.BlendshapeName);
                    if (targetIndex < 0) continue;

                    targetSmr.SetBlendShapeWeight(targetIndex, srcWeight);
                }
            }

            var clips = new HashSet<VirtualClip>();
            foreach (var key in state.BindingMappings.Keys)
            {
                foreach (var ecb in key.ToSourceEditorCurveBindings(asc))
                {
                    clips.UnionWith(animDb.GetClipsForBinding(ecb));
                }
            }

            // Walk and transform all clips
            foreach (var clip in clips)
            {
                ProcessClip(asc, clip, state);
            }
        }

        private void ProcessComponent(GameObject avatarGameObject, ModularAvatarBlendshapeSync component, BlendshapeSyncAnimationProcessorState state)
        {
            var targetSmr = component.gameObject.GetComponent<SkinnedMeshRenderer>();
            if (targetSmr == null) return;

            foreach (var binding in component.Bindings)
            {
                var refObj = binding.ReferenceMesh.Get(component);
                if (refObj == null) continue;
                var refSmr = refObj.GetComponent<SkinnedMeshRenderer>();
                if (refSmr == null) continue;

                var srcBinding = new BlendshapeSyncSummaryBinding(refSmr, binding.Blendshape);

                if (!state.BindingMappings.TryGetValue(srcBinding, out var dstBindings))
                {
                    dstBindings = new List<BlendshapeSyncSummaryBinding>();
                    state.BindingMappings[srcBinding] = dstBindings;
                }

                var targetBlendshapeName = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;

                dstBindings.Add(new BlendshapeSyncSummaryBinding(targetSmr, targetBlendshapeName));
            }
        }

        private void ProcessClip(AnimatorServicesContext asc, VirtualClip clip, BlendshapeSyncAnimationProcessorState state)
        {
            foreach (var binding in clip.GetFloatCurveBindings().ToList())
            {
                var srcBinding = BlendshapeSyncSummaryBinding.FromEditorBinding(asc, binding);
                if (srcBinding == null || !state.BindingMappings.TryGetValue(srcBinding.Value, out var dstBindings))
                {
                    continue;
                }

                var curve = clip.GetFloatCurve(binding);
                foreach (var dst in dstBindings)
                {
                    clip.SetFloatCurve(dst.ToEditorCurveBinding(asc), curve);
                }
            }
        }
    }
}

#endif