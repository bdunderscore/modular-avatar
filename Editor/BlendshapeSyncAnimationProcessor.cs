#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    /**
     * Ensures that any blendshapes marked for syncing by BlendshapeSync propagate values in all animation clips.
     *
     * Note that we only look at the FX layer, as any other layer won't work properly with mirror reflections anyway.
     */
    internal class BlendshapeSyncAnimationProcessor
    {
        private readonly ndmf.BuildContext _context;
        private Dictionary<SummaryBinding, List<(SummaryBinding target, AnimationCurve remapCurve)>> _bindingMappings;

        internal BlendshapeSyncAnimationProcessor(ndmf.BuildContext context)
        {
            _context = context;
            _bindingMappings = new Dictionary<SummaryBinding, List<(SummaryBinding target, AnimationCurve remapCurve)>>();
        }

        private struct SummaryBinding : IEquatable<SummaryBinding>
        {
            private const string PREFIX = "blendShape.";
            public readonly SkinnedMeshRenderer Renderer;
            public string propertyName => PREFIX + BlendshapeName;
            public readonly string BlendshapeName;

            public SummaryBinding(SkinnedMeshRenderer renderer, string blendShape)
            {
                Renderer = renderer;
                BlendshapeName = blendShape;
            }

            public static SummaryBinding? FromEditorBinding(AnimatorServicesContext asc, EditorCurveBinding binding)
            {
                if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(PREFIX))
                {
                    return null;
                }

                var obj = asc.ObjectPathRemapper.GetObjectForPath(binding.path);
                if (obj == null || !obj.TryGetComponent<SkinnedMeshRenderer>(out var smr)) return null;

                return new SummaryBinding(smr, binding.propertyName.Substring(PREFIX.Length));
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

            public bool Equals(SummaryBinding other)
            {
                return Renderer == other.Renderer && propertyName == other.propertyName;
            }

            public override bool Equals(object? obj)
            {
                return obj is SummaryBinding other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Renderer, propertyName);
            }
        }

        public void OnPreprocessAvatar()
        {
            var avatarGameObject = _context.AvatarRootObject;
            var asc = _context.Extension<AnimatorServicesContext>();
            var animDb = asc.AnimationIndex;
            
            _bindingMappings = new Dictionary<SummaryBinding, List<(SummaryBinding target, AnimationCurve remapCurve)>>();

            var components = avatarGameObject.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            if (components.Length == 0) return;

            foreach (var component in components)
            {
                BuildReport.ReportingObject(component, () => ProcessComponent(avatarGameObject, component));
            }

            // Apply the initial state of each binding to its targets
            foreach (var (source, targets) in _bindingMappings)
            {
                var smr = source.Renderer;
                if (smr == null) continue;

                var srcIndex = smr.sharedMesh.GetBlendShapeIndex(source.BlendshapeName);
                if (srcIndex < 0) continue;

                var srcWeight = smr.GetBlendShapeWeight(srcIndex);

                foreach (var (target, remapCurve) in targets)
                {
                    var targetSmr = target.Renderer;
                    if (targetSmr == null) continue;

                    var targetIndex = targetSmr.sharedMesh.GetBlendShapeIndex(target.BlendshapeName);
                    if (targetIndex < 0) continue;

                    var targetWeight = (remapCurve != null && remapCurve.length >= 2)
                        ? remapCurve.Evaluate(srcWeight / 100f) * 100f
                        : srcWeight;
                    targetSmr.SetBlendShapeWeight(targetIndex, targetWeight);
                }
            }

            var clips = new HashSet<VirtualClip>();
            foreach (var key in _bindingMappings.Keys)
            {
                foreach (var ecb in key.ToSourceEditorCurveBindings(asc))
                {
                    clips.UnionWith(animDb.GetClipsForBinding(ecb));
                }
            }

            // Walk and transform all clips
            foreach (var clip in clips)
            {
                ProcessClip(asc, clip);
            }
        }

        private void ProcessComponent(GameObject avatarGameObject, ModularAvatarBlendshapeSync component)
        {
            var targetSmr = component.gameObject.GetComponent<SkinnedMeshRenderer>();
            if (targetSmr == null) return;

            foreach (var binding in component.Bindings)
            {
                var refObj = binding.ReferenceMesh.Get(component);
                if (refObj == null) continue;
                var refSmr = refObj.GetComponent<SkinnedMeshRenderer>();
                if (refSmr == null) continue;

                var srcBinding = new SummaryBinding(refSmr, binding.Blendshape);

                if (!_bindingMappings.TryGetValue(srcBinding, out var dstBindings))
                {
                    dstBindings = new List<(SummaryBinding target, AnimationCurve remapCurve)>();
                    _bindingMappings[srcBinding] = dstBindings;
                }

                var targetBlendshapeName = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;

                dstBindings.Add((new SummaryBinding(targetSmr, targetBlendshapeName), binding.RemapCurve));
            }
        }

        private void ProcessClip(AnimatorServicesContext asc, VirtualClip clip)
        {
            foreach (var binding in clip.GetFloatCurveBindings().ToList())
            {
                var srcBinding = SummaryBinding.FromEditorBinding(asc, binding);
                if (srcBinding == null || !_bindingMappings.TryGetValue(srcBinding.Value, out var dstBindings))
                {
                    continue;
                }

                var curve = clip.GetFloatCurve(binding);
                foreach (var (dst, remapCurve) in dstBindings)
                {
                    if (remapCurve == null || remapCurve.length < 2)
                    {
                        clip.SetFloatCurve(dst.ToEditorCurveBinding(asc), curve);
                    }
                    else
                    {
                        const float epsilon = 0.005f;
                        var remappedCurve = new AnimationCurve();
                        foreach (var key in curve.keys)
                        {
                            var t = Mathf.Clamp01(key.value / 100f);
                            var val = remapCurve.Evaluate(t) * 100f;
                            var tPlus = Mathf.Clamp01(t + epsilon);
                            var tMinus = Mathf.Clamp01(t - epsilon);
                            var valPlus = remapCurve.Evaluate(tPlus) * 100f;
                            var valMinus = remapCurve.Evaluate(tMinus) * 100f;
                            var slope = (valPlus - valMinus) / (tPlus - tMinus);
                            remappedCurve.AddKey(new Keyframe(
                                key.time, val,
                                key.inTangent * slope,
                                key.outTangent * slope
                            ));
                        }
                        clip.SetFloatCurve(dst.ToEditorCurveBinding(asc), remappedCurve);
                    }
                }
            }
        }
    }
}

#endif