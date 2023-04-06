using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    internal static class ComponentValidation
    {
        /// <summary>
        /// Validates the provided tag component.
        /// </summary>
        /// <param name="tagComponent"></param>
        /// <returns>Null if valid, otherwise a list of configuration errors</returns>
        internal static List<ErrorLog> CheckComponent(this AvatarTagComponent tagComponent)
        {
            switch (tagComponent)
            {
                case ModularAvatarBlendshapeSync bs:
                    return CheckInternal(bs);
                case ModularAvatarBoneProxy bp:
                    return CheckInternal(bp);
                case ModularAvatarMenuInstaller mi:
                    return CheckInternal(mi);
                case ModularAvatarMergeAnimator obj:
                    return CheckInternal(obj);
                case ModularAvatarMergeArmature obj:
                    return CheckInternal(obj);
                default:
                    return null;
            }
        }

        internal static List<ErrorLog> ValidateAll(GameObject root)
        {
            List<ErrorLog> logs = new List<ErrorLog>();
            foreach (var component in root.GetComponentsInChildren<AvatarTagComponent>(true))
            {
                var componentLogs = component.CheckComponent();
                if (componentLogs != null)
                {
                    logs.AddRange(componentLogs);
                }
            }

            return logs;
        }

        private static List<ErrorLog> CheckInternal(ModularAvatarBlendshapeSync bs)
        {
            var localMesh = bs.GetComponent<SkinnedMeshRenderer>();
            if (localMesh == null)
            {
                return new List<ErrorLog>
                    {new ErrorLog(ReportLevel.Validation, "validation.blendshape_sync.no_local_renderer", bs)};
            }

            if (localMesh.sharedMesh == null)
            {
                return new List<ErrorLog>
                    {new ErrorLog(ReportLevel.Validation, "validation.blendshape_sync.no_local_mesh", bs)};
            }

            if (bs.Bindings == null || bs.Bindings.Count == 0)
            {
                return new List<ErrorLog>
                    {new ErrorLog(ReportLevel.Validation, "validation.blendshape_sync.no_bindings", bs)};
            }

            List<ErrorLog> errorLogs = new List<ErrorLog>();
            foreach (var binding in bs.Bindings)
            {
                var localShape = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;

                if (localMesh.sharedMesh.GetBlendShapeIndex(localShape) == -1)
                {
                    errorLogs.Add(new ErrorLog(ReportLevel.Validation, "validation.blendshape_sync.missing_local_shape",
                        new string[] {localShape}, bs));
                }

                var targetObj = binding.ReferenceMesh.Get(bs.transform);
                if (targetObj == null)
                {
                    errorLogs.Add(new ErrorLog(ReportLevel.Validation, "validation.blendshape_sync.no_target", bs));
                    continue;
                }

                var targetRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();
                if (targetRenderer == null)
                {
                    errorLogs.Add(new ErrorLog(ReportLevel.Validation,
                        "validation.blendshape_sync.missing_target_renderer", bs, targetRenderer));
                    continue;
                }

                var targetMesh = targetRenderer.sharedMesh;
                if (targetMesh == null)
                {
                    errorLogs.Add(new ErrorLog(ReportLevel.Validation, "validation.blendshape_sync.missing_target_mesh",
                        bs, targetRenderer));
                    continue;
                }

                if (targetMesh.GetBlendShapeIndex(binding.Blendshape) == -1)
                {
                    errorLogs.Add(new ErrorLog(ReportLevel.Validation,
                        "validation.blendshape_sync.missing_target_shape", new string[] {binding.Blendshape}, bs,
                        targetRenderer));
                }
            }

            if (errorLogs.Count == 0)
            {
                return null;
            }
            else
            {
                return errorLogs;
            }
        }

        private static List<ErrorLog> CheckInternal(ModularAvatarBoneProxy bp)
        {
            if (bp.target == null)
            {
                return new List<ErrorLog>()
                {
                    new ErrorLog(ReportLevel.Validation, "validation.bone_proxy.no_target", bp)
                };
            }

            return null;
        }

        private static List<ErrorLog> CheckInternal(ModularAvatarMenuInstaller mi)
        {
            // TODO - check that target menu is in the avatar
            if (mi.menuToAppend == null)
            {
                return new List<ErrorLog>()
                {
                    new ErrorLog(ReportLevel.Validation, "validation.menu_installer.no_menu", mi)
                };
            }

            return null;
        }

        private static List<ErrorLog> CheckInternal(ModularAvatarMergeAnimator ma)
        {
            foreach (AnimLayerData data in ma.animators)
            {
                if (data.animator == null)
                {
                    return new List<ErrorLog>()
                {
                    new ErrorLog(ReportLevel.Validation, "validation.merge_animator.no_animator", ma)
                };
                }
            }

            return null;
        }

        private static List<ErrorLog> CheckInternal(ModularAvatarMergeArmature ma)
        {
            if (ma.mergeTargetObject == null)
            {
                return new List<ErrorLog>()
                {
                    new ErrorLog(ReportLevel.Validation, "validation.merge_armature.no_target", ma)
                };
            }

            if (ma.mergeTargetObject == ma.gameObject || ma.mergeTargetObject.transform.IsChildOf(ma.transform))
            {
                return new List<ErrorLog>()
                {
                    new ErrorLog(ReportLevel.Validation, "error.merge_armature.merge_into_self", ma,
                        ma.mergeTargetObject)
                };
            }

            return null;
        }
    }
}