using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class Localization
    {
        public static event Action OnLangChange;
        private const string FallbackLanguage = "en";

        private const string localizationPathGuid = "488c994003974b3ab2796371cf627bca";
        private static string localizationPathRoot = AssetDatabase.GUIDToAssetPath(localizationPathGuid);

        private static ImmutableDictionary<string, string> SupportedLanguageDisplayNames
            = ImmutableDictionary<string, string>.Empty
                .Add("en-us", "English")
                .Add("ja-jp", "日本語")
                .Add("zh-hans", "简体中文")
                .Add("ko-kr", "한국어");

        private static ImmutableList<string>
            SupportedLanguages = new string[] {"en-us", "ja-jp", "zh-hans", "ko-kr"}.ToImmutableList();

        private static string[] DisplayNames = SupportedLanguages.Select(l =>
        {
            return SupportedLanguageDisplayNames.TryGetValue(l, out var displayName) ? displayName : l;
        }).ToArray();

        private static Dictionary<string, ImmutableDictionary<string, string>> Cache
            = new Dictionary<string, ImmutableDictionary<string, string>>();

        internal static string OverrideLanguage { get; set; } = null;

        public static Localizer L { get; private set; }

        static Localization()
        {
            Localizer localizer = new Localizer(SupportedLanguages[0], () =>
            {
                List<(string, Func<string, string>)> languages = new List<(string, Func<string, string>)>();
                
                foreach (var lang in SupportedLanguages)
                {
                    languages.Add((lang, LanguageLookup(lang)));
                }

                return languages;
            });
            
            L = localizer;
            
            LanguagePrefs.RegisterLanguageChangeCallback(typeof(Localization), _ => OnLangChange?.Invoke());
        }

        private static Func<string,string> LanguageLookup(string lang)
        {
            var filename = localizationPathRoot + "/" + lang + ".json";

            try
            {
                var langData = File.ReadAllText(filename);
                var langMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(langData);

                // return langMap.GetValueOrDefault; - Unity 2019 doesn't have this extension method
                return key =>
                {
                    if (langMap.TryGetValue(key, out var val)) return val;
                    else return null;
                };
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load language file " + filename);
                Debug.LogException(e);
                return (k) => null;
            }
        }

        [MenuItem("Tools/Modular Avatar/Reload localizations")]
        public static void Reload()
        {
            Localizer.ReloadLocalizations();
            Cache.Clear();
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
        
        public static string S_f(string key, params string[] format)
        {
            try
            {
                return string.Format(S(key, key), format);
            }
            catch (FormatException)
            {
                return S(key, key) + "(" + string.Join(", ", format) + ")";
            }
        }

        public static string S(string key, string defValue)
        {
            if (L.TryGetLocalizedString(key, out var val))
            {
                return val;
            }
            else
            {
                return defValue;
            }
        }

        public static string GetSelectedLocalization()
        {
            return LanguagePrefs.Language;
        }

        public static void ShowLanguageUI()
        {
            EditorGUILayout.Separator();
            
            LanguageSwitcher.DrawImmediate();
        }
    }
}
