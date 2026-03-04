using System;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using UnityEngine;
using BuildReport = nadena.dev.modular_avatar.editor.ErrorReporting.BuildReport;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class FloorAdjusterPass : Pass<FloorAdjusterPass>
    {
        internal static void TestExecute(ndmf.BuildContext context)
        {
            Instance.Execute(context);
        }

        protected override void Execute(ndmf.BuildContext context)
        {
            var adjusters = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarFloorAdjuster>(true);
            var activeAdjusters = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarFloorAdjuster>(false);

            if (adjusters.Length == 0 || activeAdjusters.Length == 0) return;
            if (activeAdjusters.Length > 1)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "error.floor_adjuster.multiple_active", activeAdjusters);
                return;
            }

            if (adjusters.Length > 1 && activeAdjusters.Length == 1)
            {
                // warning
                BuildReport.Log(ErrorSeverity.NonFatal, "error.floor_adjuster.multiple_inactive", adjusters);
            }

            var target = activeAdjusters[0];
            var animator = context.AvatarRootObject.GetComponent<Animator>();
            if (animator == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "error.floor_adjuster.no_animator", target);
                return;
            }

            Transform hips;
            try
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);

                if (hips == null || hips == context.AvatarRootTransform || hips.parent == null)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal, "error.floor_adjuster.no_hips", target);
                    return;
                }
            }
            // GetBoneTransform throws an exception when the avatar is missing (despite documenting returning null)
            catch (Exception)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "error.floor_adjuster.no_hips", target);
                return;
            }

            var hipsParent = hips.parent;

            var currentFloor = hipsParent.worldToLocalMatrix.MultiplyPoint(context.AvatarRootTransform.position);
            var newFloor = hipsParent.worldToLocalMatrix.MultiplyPoint(target.transform.position);
            var adjustment = newFloor - currentFloor;

            // Ensure we only adjust in the Y axis
            adjustment = Vector3.Project(adjustment, hipsParent.worldToLocalMatrix.MultiplyVector(Vector3.up));

            var eyeAdjustment = context.AvatarRootTransform.worldToLocalMatrix.MultiplyPoint(target.transform.position);
            eyeAdjustment.x = 0;
            eyeAdjustment.z = 0;

            var cai = context.PlatformProvider.ExtractCommonAvatarInfo(context.AvatarRootObject);
            if (context.PlatformProvider != GenericPlatform.Instance)
            {
                cai?.MergeFrom(GenericPlatform.Instance.ExtractCommonAvatarInfo(context.AvatarRootObject));
            }

            if (cai == null || cai.EyePosition == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "error.floor_adjuster.no_eye", target);
                return;
            }

            var hipsLocalPos = hips.localPosition - adjustment;
            hips.localPosition = hipsLocalPos;

            // We only need to write back the eye position, not the other properties
            var newCai = new CommonAvatarInfo();
            newCai.EyePosition = cai.EyePosition - eyeAdjustment;
            context.PlatformProvider.InitBuildFromCommonAvatarInfo(context, newCai);

            HumanoidAvatarDescriptorRebuilder.Rebuild(context, (hbm, humanDesc) =>
            {
                if (hbm.HumanBoneEnum == HumanBodyBones.Hips && hbm.Bone != null)
                {
                    hbm.Bone.localPosition = hips.localPosition;
                    humanDesc.skeleton[hbm.BoneIndex].position = hbm.Bone.localPosition;
                }
            });

            // Rebinding the avatar can reset the hips position, set localPosition again now
            hips.localPosition = hipsLocalPos;
        }
    }
}