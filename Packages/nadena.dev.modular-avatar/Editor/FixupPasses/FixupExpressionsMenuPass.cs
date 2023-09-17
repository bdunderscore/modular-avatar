using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class FixupExpressionsMenuPass
    {
        internal static void FixupExpressionsMenu(BuildContext context)
        {
            var expressionsMenu = context.AvatarDescriptor.expressionsMenu;
            if (expressionsMenu == null) return;
            
            var parameters = context.AvatarDescriptor.expressionParameters?.parameters
                             ?? new VRCExpressionParameters.Parameter[0];
            var parameterNames = parameters.Select(p=> p.name).ToImmutableHashSet();

            if (!Util.IsTemporaryAsset(expressionsMenu))
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

                    foreach (var subParam in control.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>())
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

        #if UNITY_ANDROID
        private const TextureFormat TargetFormat = TextureFormat.ASTC_4x4;
        #else
        private const TextureFormat TargetFormat = TextureFormat.DXT5;
        #endif
        
        private static Texture2D MaybeScaleIcon(BuildContext context, Texture2D original)
        {
            if (original.width <= 256 && original.height <= 256 && IsCompressedFormat(original.format))
            {
                return original;
            }
            
            var newRatio = Math.Min(256f / original.width, 256f / original.height);
            var newWidth = Math.Min(256, Mathf.RoundToInt(original.width * newRatio));
            var newHeight = Math.Min(256, Mathf.RoundToInt(original.height * newRatio));

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