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
        private Dictionary<SummaryBinding, List<SummaryBinding>> _bindingMappings;

        internal BlendshapeSyncAnimationProcessor(ndmf.BuildContext context)
        {
            _context = context;
            _bindingMappings = new Dictionary<SummaryBinding, List<SummaryBinding>>();
        }

        private struct SummaryBinding : IEquatable<SummaryBinding>
        {
            private const string PREFIX = "blendShape.";
            public string path;
            public string propertyName;

            public SummaryBinding(string path, string blendShape)
            {
                this.path = path;
                this.propertyName = PREFIX + blendShape;
            }

            public static SummaryBinding? FromEditorBinding(EditorCurveBinding binding)
            {
                if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(PREFIX))
                {
                    return null;
                }

                return new SummaryBinding(binding.path, binding.propertyName.Substring(PREFIX.Length));
            }

            public EditorCurveBinding ToEditorCurveBinding()
            {
                return EditorCurveBinding.FloatCurve(
                    path,
                    typeof(SkinnedMeshRenderer),
                    propertyName
                );
            }

            public bool Equals(SummaryBinding other)
            {
                return path == other.path && propertyName == other.propertyName;
            }

            public override bool Equals(object? obj)
            {
                return obj is SummaryBinding other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(path, propertyName);
            }
        }

        public void OnPreprocessAvatar()
        {
            var avatarGameObject = _context.AvatarRootObject;
            var animDb = _context.Extension<AnimatorServicesContext>().AnimationIndex;
            
            _bindingMappings = new Dictionary<SummaryBinding, List<SummaryBinding>>();

            var components = avatarGameObject.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            if (components.Length == 0) return;

            foreach (var component in components)
            {
                BuildReport.ReportingObject(component, () => ProcessComponent(avatarGameObject, component));
            }

            var clips = new HashSet<VirtualClip>();
            foreach (var key in _bindingMappings.Keys)
            {
                var ecb = key.ToEditorCurveBinding();
                clips.UnionWith(animDb.GetClipsForBinding(ecb));
            }

            // Walk and transform all clips
            foreach (var clip in clips)
            {
                ProcessClip(clip);
            }
        }

        private void ProcessComponent(GameObject avatarGameObject, ModularAvatarBlendshapeSync component)
        {
            var targetObj = RuntimeUtil.RelativePath(avatarGameObject, component.gameObject);

            if (targetObj == null) return;

            foreach (var binding in component.Bindings)
            {
                var refObj = binding.ReferenceMesh.Get(component);
                if (refObj == null) continue;
                var refSmr = refObj.GetComponent<SkinnedMeshRenderer>();
                if (refSmr == null) continue;

                var refPath = RuntimeUtil.RelativePath(avatarGameObject, refObj);
                if (refPath == null) continue;

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

        private void ProcessClip(VirtualClip clip)
        {
            foreach (var binding in clip.GetFloatCurveBindings().ToList())
            {
                var srcBinding = SummaryBinding.FromEditorBinding(binding);
                if (srcBinding == null || !_bindingMappings.TryGetValue(srcBinding.Value, out var dstBindings))
                {
                    continue;
                }

                var curve = clip.GetFloatCurve(binding);
                foreach (var dst in dstBindings)
                {
                    clip.SetFloatCurve(dst.ToEditorCurveBinding(), curve);
                }
            }
        }
    }
}

#endif