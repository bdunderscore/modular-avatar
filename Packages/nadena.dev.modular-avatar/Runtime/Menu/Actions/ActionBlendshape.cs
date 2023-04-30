using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Action Blendshape")]
    [RequireComponent(typeof(ActionController))]
    public class ActionBlendshape : AvatarTagComponent, SwitchedMenuAction
    {
        [Serializable]
        public class BlendshapeSpec
        {
            public AvatarObjectReference target = new AvatarObjectReference();
            public string blendshape;
            public float value;
        }

        public List<BlendshapeSpec> Blendshapes;

        public bool BindsParameter(TargetParameter parameter)
        {
            return parameter == TargetParameter.BaseParameter;
        }

        public ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetCurves()
        {
            ImmutableDictionary<MenuCurveBinding, AnimationCurve>.Builder builder =
                ImmutableDictionary<MenuCurveBinding, AnimationCurve>.Empty.ToBuilder();

            foreach (var spec in Blendshapes)
            {
                var target = spec.target?.Get(this);
                if (target == null) continue;

                if (string.IsNullOrWhiteSpace(spec.blendshape)) continue;

                builder.Add(
                    new MenuCurveBinding(target, typeof(SkinnedMeshRenderer), "blendShape." + spec.blendshape),
                    AnimationCurve.Constant(0, 1, spec.value)
                );
            }

            return builder.ToImmutable();
        }

        public ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetInactiveCurves()
        {
            ImmutableDictionary<MenuCurveBinding, AnimationCurve>.Builder builder =
                ImmutableDictionary<MenuCurveBinding, AnimationCurve>.Empty.ToBuilder();

            foreach (var spec in Blendshapes)
            {
                var target = spec.target?.Get(this);
                if (target == null) continue;

                var targetRenderer = target.GetComponent<SkinnedMeshRenderer>();
                if (targetRenderer == null) continue;

                var mesh = targetRenderer.sharedMesh;
                if (mesh == null) continue;

                if (string.IsNullOrWhiteSpace(spec.blendshape)) continue;

                var blendshapeIndex = mesh.GetBlendShapeIndex(spec.blendshape);
                if (blendshapeIndex < 0) continue;

                var value = targetRenderer.GetBlendShapeWeight(blendshapeIndex);

                builder.Add(
                    new MenuCurveBinding(target, typeof(SkinnedMeshRenderer), "blendShape." + spec.blendshape),
                    AnimationCurve.Constant(0, 1, value)
                );
            }

            return builder.ToImmutable();
        }
    }
}