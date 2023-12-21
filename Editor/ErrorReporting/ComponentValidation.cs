using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;

#if MA_VRCSDK3_AVATARS
using nadena.dev.modular_avatar.core.menu;
#endif

using UnityEngine;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    internal static class ComponentValidation
    {
        /// <summary>
        /// Validates the provided tag component.
        /// </summary>
        /// <param name="tagComponent"></param>
        /// <returns>Null if valid, otherwise a list of configuration errors</returns>
        internal static void CheckComponent(this AvatarTagComponent tagComponent)
        {
            ErrorReport.WithContextObject(tagComponent, () =>
            {
                switch (tagComponent)
                {
                    case ModularAvatarBlendshapeSync bs:
                        CheckInternal(bs);
                        break;
                    case ModularAvatarBoneProxy bp:
                        CheckInternal(bp);
                        break;
#if MA_VRCSDK3_AVATARS
                    case ModularAvatarMenuInstaller mi:
                        CheckInternal(mi);
                        break;
                    case ModularAvatarMergeAnimator obj:
                        CheckInternal(obj);
                        break;
#endif
                    case ModularAvatarMergeArmature obj:
                        CheckInternal(obj);
                        break;
                    default:
                        return;
                }
            });
        }

        internal static void ValidateAll(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<AvatarTagComponent>(true))
            {
                component.CheckComponent();
            }
        }

        private static void CheckInternal(ModularAvatarBlendshapeSync bs)
        { 
            var localMesh = bs.GetComponent<SkinnedMeshRenderer>();
            if (localMesh == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "validation.blendshape_sync.no_local_renderer", bs);
            }

            if (localMesh.sharedMesh == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "validation.blendshape_sync.no_local_mesh", bs);
            }

            if (bs.Bindings == null || bs.Bindings.Count == 0)
            {
                BuildReport.Log(ErrorSeverity.Information,"validation.blendshape_sync.no_bindings", bs);
            }

            foreach (var binding in bs.Bindings)
            {
                var localShape = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;

                if (localMesh.sharedMesh.GetBlendShapeIndex(localShape) == -1)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal, "validation.blendshape_sync.missing_local_shape",
                        localShape, bs);
                }

                var targetObj = binding.ReferenceMesh.Get(bs.transform);
                if (targetObj == null)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal, "validation.blendshape_sync.no_target", bs);
                    continue;
                }

                var targetRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();
                if (targetRenderer == null)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal,
                        "validation.blendshape_sync.missing_target_renderer", bs, targetRenderer);
                    continue;
                }

                var targetMesh = targetRenderer.sharedMesh;
                if (targetMesh == null)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal, "validation.blendshape_sync.missing_target_mesh",
                        bs, targetRenderer);
                    continue;
                }

                if (targetMesh.GetBlendShapeIndex(binding.Blendshape) == -1)
                {
                    BuildReport.Log(ErrorSeverity.NonFatal,
                        "validation.blendshape_sync.missing_target_shape", binding.Blendshape, bs,
                        targetRenderer);
                }
            }
        }

        private static void CheckInternal(ModularAvatarBoneProxy bp)
        {
            if (bp.target == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "validation.bone_proxy.no_target", bp);
            }
        }

#if MA_VRCSDK3_AVATARS
        private static void CheckInternal(ModularAvatarMenuInstaller mi)
        {
            // TODO - check that target menu is in the avatar
            if (mi.menuToAppend == null && mi.GetComponent<MenuSource>() == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "validation.menu_installer.no_menu", mi);
            }
        }

        private static void CheckInternal(ModularAvatarMergeAnimator ma)
        {
            if (ma.animator == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "validation.merge_animator.no_animator", ma);
            }
        }
#endif

        private static void CheckInternal(ModularAvatarMergeArmature ma)
        {
            if (ma.mergeTargetObject == null)
            {
                BuildReport.Log(ErrorSeverity.NonFatal, "validation.merge_armature.no_target", ma);
                return;
            }

            if (ma.mergeTargetObject == ma.gameObject || ma.mergeTargetObject.transform.IsChildOf(ma.transform))
            {
                BuildReport.Log(ErrorSeverity.Error, "error.merge_armature.circular_dependency", ma,
                    ma.mergeTargetObject);
            }
        }
    }
}