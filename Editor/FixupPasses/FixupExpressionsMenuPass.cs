#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class FixupExpressionsMenuPass
    {
        private const string DEFAULT_EXP_MENU_GUID = "024fb8ef5b3988c46b446863c92f4522";
        private const string DEFAULT_EXP_PARAM_GUID = "03a6d797deb62f0429471c4e17ea99a7";

        internal static void FixupExpressionsMenu(BuildContext context)
        {
            if (!context.AvatarDescriptor) return;

            context.AvatarDescriptor.customExpressions = true;

            var expressionsMenu = context.AvatarDescriptor.expressionsMenu;
            if (expressionsMenu == null)
            {
                var defaultExpMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(
                    AssetDatabase.GUIDToAssetPath(DEFAULT_EXP_MENU_GUID)
                );

                expressionsMenu = Object.Instantiate(defaultExpMenu);
                context.AvatarDescriptor.expressionsMenu = expressionsMenu;
            }

            if (context.AvatarDescriptor.expressionParameters == null)
            {
                var defaultExpParam = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(
                    AssetDatabase.GUIDToAssetPath(DEFAULT_EXP_PARAM_GUID)
                );

                context.AvatarDescriptor.expressionParameters = Object.Instantiate(defaultExpParam);
            }

            var parameters = context.AvatarDescriptor.expressionParameters.parameters
                             ?? Array.Empty<VRCExpressionParameters.Parameter>();
            var parameterNames = parameters.Select(p => p.name).ToImmutableHashSet();

            if (!context.PluginBuildContext.IsTemporaryAsset(expressionsMenu))
            {
                expressionsMenu = context.CloneMenu(expressionsMenu);
                context.AvatarDescriptor.expressionsMenu = expressionsMenu;
            }

            // Walk menu recursively
            var visitedMenus = new HashSet<VRCExpressionsMenu>();
            var iconMapping = new Dictionary<Texture2D, Texture2D>();

            VisitMenu(expressionsMenu);

            void VisitMenu(VRCExpressionsMenu menu)
            {
                if (!visitedMenus.Add(menu)) return;

                foreach (var control in menu.controls)
                {
                    if (control.parameter != null &&
                        !string.IsNullOrEmpty(control.parameter.name) &&
                        !parameterNames.Contains(control.parameter.name))
                    {
                        control.parameter.name = "";
                    }

                    foreach (var subParam in control.subParameters ??
                                             Array.Empty<VRCExpressionsMenu.Control.Parameter>())
                    {
                        if (subParam != null &&
                            !string.IsNullOrEmpty(subParam.name) &&
                            !parameterNames.Contains(subParam.name))
                        {
                            subParam.name = "";
                        }
                    }

                    if (control.icon != null)
                    {
                        if (!iconMapping.TryGetValue(control.icon, out var newIcon))
                        {
                            iconMapping[control.icon] = newIcon = MaybeScaleIcon(context, control.icon);
                        }

                        control.icon = newIcon;
                    }

                    if (control.subMenu != null)
                    {
                        VisitMenu(control.subMenu);
                    }

                    if (control.labels != null)
                    {
                        for (int i = 0; i < control.labels.Length; i++)
                        {
                            var label = control.labels[i];

                            if (label.icon != null)
                            {
                                if (!iconMapping.TryGetValue(label.icon, out var newIcon))
                                {
                                    iconMapping[label.icon] = newIcon = MaybeScaleIcon(context, label.icon);
                                }

                                label.icon = newIcon;
                                control.labels[i] = label;
                            }
                        }
                    }
                }
            }
        }

        internal static TextureFormat TargetFormat
        {
            get
            {
                switch (EditorUserBuildSettings.activeBuildTarget)
                {
                    case BuildTarget.StandaloneWindows64:
                        return TextureFormat.DXT5;
                    default:
                        return TextureFormat.ASTC_4x4;
                }
            }
        }
        

        private static Texture2D MaybeScaleIcon(BuildContext context, Texture2D original)
        {
            if (original.width <= 256 && original.height <= 256 && IsCompressedFormat(original.format))
            {
                return original;
            }

            var newRatio = Math.Min(1, Math.Min(256f / original.width, 256f / original.height));
            var newWidth = Math.Min(256, Mathf.RoundToInt(original.width * newRatio));
            var newHeight = Math.Min(256, Mathf.RoundToInt(original.height * newRatio));

            // Round up to a multiple of four
            newWidth = (newWidth + 3) & ~3;
            newHeight = (newHeight + 3) & ~3;

            var newTex = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, true);
            context.SaveAsset(newTex);

            var tmpRenderTex = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            var originalActiveRenderTex = RenderTexture.active;

            try
            {
                Graphics.Blit(original, tmpRenderTex);
                RenderTexture.active = tmpRenderTex;
                newTex.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                newTex.Apply();
                EditorUtility.CompressTexture(newTex, TargetFormat, TextureCompressionQuality.Normal);

                return newTex;
            }
            finally
            {
                RenderTexture.active = originalActiveRenderTex;
                RenderTexture.ReleaseTemporary(tmpRenderTex);
            }
        }

        private static bool IsCompressedFormat(TextureFormat format)
        {
            var name = format.ToString();
            return name.StartsWith("DXT") || name.StartsWith("ASTC");
        }
    }
}

#endif
