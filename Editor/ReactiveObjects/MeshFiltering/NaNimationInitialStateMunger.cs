using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Configures the initial state of NaNimation bones.
    ///
    /// Because we cannot configure the initial scale of a bone to be NaN, we instead use VRCScaleConstraints
    /// to animate those bones to NaN when user animations are disabled. When user animations are enabled,
    /// we turn off the constraints, which allows normal NaNimations to take over.
    /// </summary>
    internal static class NaNimationInitialStateMunger
    {
        public static void ApplyInitialStates(
            Transform avatarRoot,
            IEnumerable<GameObject> initiallyActive,
            VirtualClip baseStateClip
        )
        {
            foreach (var initialBone in initiallyActive)
            {
                if (initialBone == null) continue;

                var constraint = initialBone.AddComponent<VRCScaleConstraint>();
                constraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = constraint.transform,
                    Weight = float.NaN
                });
                constraint.GlobalWeight = 1.0f;
                constraint.Locked = true;
                constraint.IsActive = true;

                baseStateClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve(
                        RuntimeUtil.AvatarRootPath(constraint.gameObject),
                        typeof(VRCScaleConstraint),
                        "IsActive"
                    ),
                    // Disable the constraint when animations are active
                    AnimationCurve.Constant(0, 1, 0.0f)
                );
            }
        }
    }
}

#endif