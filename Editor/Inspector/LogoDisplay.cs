#region

using System;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class LogoDisplay
    {
        internal static readonly Texture2D LOGO_ASSET;
        internal static float TARGET_HEIGHT
        {
            get {
                try
                {
                    return (EditorStyles.label?.lineHeight ?? 0) * 3;
                }
                catch (NullReferenceException)
                {
                    // This can happen in early initialization...
                    return 0;
                }
            }
        }

        internal static float ImageWidth(float height)
        {
            return (height / (float) LOGO_ASSET.height) * LOGO_ASSET.width;
        }

        private static GUIStyle STYLE => new GUIStyle()
        {
            fixedHeight = TARGET_HEIGHT,
            fixedWidth = TARGET_HEIGHT * (LOGO_ASSET.width / (float) LOGO_ASSET.height),
            stretchHeight = false,
            stretchWidth = false,
            imagePosition = ImagePosition.ImageOnly
        };

        static LogoDisplay()
        {
            var placeholderPath = AssetDatabase.GUIDToAssetPath("2a2bb4e0b8e906743890ef10c778e65c");

            var path = placeholderPath.Substring(0, placeholderPath.LastIndexOf("/", StringComparison.Ordinal));
            path += "/ma_logo.png";

            var real_logo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (real_logo != null) LOGO_ASSET = real_logo;
            else LOGO_ASSET = AssetDatabase.LoadAssetAtPath<Texture2D>(placeholderPath);
        }

        internal static void DisplayLogo()
        {
            if (LOGO_ASSET == null || EditorStyles.label == null) return; 

            var height = TARGET_HEIGHT;
            var width = ImageWidth(height);
            var rect = GUILayoutUtility.GetRect(width, height);

            GUI.DrawTexture(rect, LOGO_ASSET, ScaleMode.ScaleToFit);
            GUILayoutUtility.GetRect(width, EditorStyles.label.lineHeight / 2);
        }
    }
}