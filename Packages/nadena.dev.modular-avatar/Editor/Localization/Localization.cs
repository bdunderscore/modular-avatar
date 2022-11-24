using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    public static class Localization
    {
        private const string localizationPathGuid = "488c994003974b3ab2796371cf627bca";
        private static string localizationPathRoot = AssetDatabase.GUIDToAssetPath(localizationPathGuid);

        private static ImmutableDictionary<string, string> SupportedLanguageDisplayNames
            = ImmutableDictionary<string, string>.Empty
                .Add("en", "English")
                .Add("ja", "日本語");

        private static ImmutableList<string> SupportedLanguages = new string[] {"en", "ja"}.ToImmutableList();

        private static string[] DisplayNames = SupportedLanguages.Select(l =>
        {
            return SupportedLanguageDisplayNames.TryGetValue(l, out var displayName) ? displayName : l;
        }).ToArray();

        private static Dictionary<string, ImmutableDictionary<string, string>> Cache
            = new Dictionary<string, ImmutableDictionary<string, string>>();

        [MenuItem("Tools/Modular Avatar/Reload localizations")]
        public static void Reload()
        {
            Cache.Clear();
        }

        private static ImmutableDictionary<string, string> GetLocalization(string lang)
        {
            if (Cache.TryGetValue(lang, out var info))
            {
                return info;
            }

            var filename = localizationPathRoot + "/" + lang + ".json";

            try
            {
                var langData = File.ReadAllText(filename);
                info = JsonConvert.DeserializeObject<Dictionary<string, string>>(langData).ToImmutableDictionary();
                Cache[lang] = info;
                return info;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load language file " + filename);
                Debug.LogException(e);
                return ImmutableDictionary<string, string>.Empty;
            }
        }

        public static GUIContent G(string key)
        {
            var tooltip = S(key + ".tooltip", null);
            return tooltip != null ? new GUIContent(S(key), tooltip) : new GUIContent(S(key));
        }

        public static string S(string key)
        {
            return S(key, key);
        }

        public static string S(string key, string defValue)
        {
            var info = GetLocalization(GetSelectedLocalization());

            if (info.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                return defValue;
            }
        }

        public static string GetSelectedLocalization()
        {
            return EditorPrefs.GetString("nadena.dev.modularavatar.lang", "en");
        }

        public static void ShowLanguageUI()
        {
            EditorGUILayout.Separator();

            var curLang = GetSelectedLocalization();

            var curIndex = SupportedLanguages.IndexOf(curLang);
            var newIndex = EditorGUILayout.Popup("Editor Language", curIndex, DisplayNames);
            if (newIndex != curIndex)
            {
                EditorPrefs.SetString("nadena.dev.modularavatar.lang", SupportedLanguages[newIndex]);
                Reload();
            }
        }
    }
}