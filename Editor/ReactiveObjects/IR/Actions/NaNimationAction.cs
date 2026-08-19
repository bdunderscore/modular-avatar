#nullable enable

#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class NaNimationAction : IAction
    {
        private readonly TargetProp _targetProp;
        private readonly List<GameObject> _bones;
        private readonly bool _shouldDelete;

        public NaNimationAction(TargetProp targetProp, List<GameObject> bones, bool shouldDelete)
        {
            _targetProp = targetProp;
            _bones = bones;
            _shouldDelete = shouldDelete;
        }

        public object TargetKey => _targetProp;

        public void ToMotion(BakeContext context, VirtualClip clip)
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0, _shouldDelete ? float.NaN : 1.0f));

            foreach (var bone in _bones)
            {
                var path = context.ObjectPathRemapper.GetVirtualPathForObject(bone);
                foreach (var dim in new[] { "x", "y", "z" })
                {
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalScale.{dim}"),
                        curve
                    );
                }
            }

            if (_shouldDelete && _targetProp.TargetObject is SkinnedMeshRenderer smr)
            {
                // Suppress AABB recalculation warnings caused by NaN vertex positions
                clip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve(
                        context.ObjectPathRemapper.GetVirtualPathForObject(smr.gameObject),
                        typeof(SkinnedMeshRenderer),
                        "m_UpdateWhenOffscreen"
                    ),
                    AnimationCurve.Constant(0, 1, 0)
                );
            }
        }

        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            // Always write retain (scale=1) as the base/default state for all bones
            var retainCurve = AnimationCurve.Constant(0, 1, 1.0f);
            foreach (var bone in _bones)
            {
                var path = context.ObjectPathRemapper.GetVirtualPathForObject(bone);
                foreach (var dim in new[] { "x", "y", "z" })
                {
                    context.BaseLayerClip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalScale.{dim}"),
                        retainCurve
                    );
                }
            }

            if (actionStartsActive)
            {
                // The shape starts deleted. Since we can't initialize bone scale to NaN directly,
                // add a VRCScaleConstraint whose source weight is NaN — this keeps the bone at
                // NaN scale before user animations take over. The base clip disables the constraint
                // (IsActive=0) so normal NaNimation clips can drive scale once animations are active.
                foreach (var bone in _bones)
                {
                    var constraint = bone.AddComponent<VRCScaleConstraint>();
                    constraint.Sources.Add(new VRCConstraintSource
                    {
                        SourceTransform = constraint.transform,
                        Weight = float.NaN
                    });
                    constraint.GlobalWeight = 1.0f;
                    constraint.Locked = true;
                    constraint.IsActive = true;

                    context.BaseLayerClip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(
                            context.ObjectPathRemapper.GetVirtualPathForObject(bone),
                            typeof(VRCScaleConstraint),
                            "IsActive"
                        ),
                        AnimationCurve.Constant(0, 1, 0.0f)
                    );
                }
            }
        }
    }
}

#endif