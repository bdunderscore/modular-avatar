using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

#if MA_VRCSDK3_AVATARS

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Configures the initial state of NaNimation bones.
    ///
    /// Because we cannot configure the initial scale of a bone to be NaN, we instead use VRCScaleConstraints
    /// to animate those bones to NaN when user animations are disabled. When user animations are enabled,
    /// we turn off the constraints, which allows normal NaNimations to take over.
    /// </summary>
    internal static class NaNimationConstraintMunger
    {
                    
#if MA_HAS_VRCFURY
            const bool HasVrcFury = true;
#else
            const bool HasVrcFury = false;
#endif

        
        public static void ApplyConstraints(
            Transform avatarRoot,
            Dictionary<string, List<GameObject>> nanplan,
            List<string> initiallyActiveList,
            VirtualClip baseStateClip,
            Dictionary<string, VirtualClip> activationClips
        )
        {
            var initiallyActiveSet = initiallyActiveList.ToHashSet();
            var boneToKey = nanplan
                .SelectMany(kv => kv.Value.Select(bone => (bone, kv.Key)))
                .ToDictionary(kv => kv.bone, kv => kv.Key);

            foreach (var kv in boneToKey)
            {
                var (bone, key) = kv;
                bool initiallyActive = initiallyActiveSet.Contains(key);

                if (!initiallyActive && !HasVrcFury) continue;
                
                var constraint = bone.AddComponent<VRCScaleConstraint>();
                constraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = constraint.transform,
                    Weight = float.NaN
                });
                constraint.GlobalWeight = initiallyActive ? 1.0f : 0.0f;
                constraint.Locked = true;
                constraint.IsActive = true;

                if (!HasVrcFury)
                {
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
                else
                {
                    baseStateClip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(
                            RuntimeUtil.AvatarRootPath(constraint.gameObject),
                            typeof(VRCScaleConstraint),
                            "GlobalWeight"
                        ),
                        AnimationCurve.Constant(0, 1, 0.0f)
                    );
                }

                if (activationClips.TryGetValue(key, out var clip))
                {
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve(
                            RuntimeUtil.AvatarRootPath(constraint.gameObject),
                            typeof(VRCScaleConstraint),
                            "GlobalWeight"
                        ),
                        AnimationCurve.Constant(0, 1, 1.0f)
                    );
                }
            }
        }
    }
}

#endif