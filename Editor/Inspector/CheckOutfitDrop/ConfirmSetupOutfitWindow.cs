#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class SetupOutfitWindow : EditorWindow
    {
        private static SetupOutfitWindow? _instance;
        private static readonly List<GameObject> _entries = new();

        private const float MinWidth = 400f;
        private const float MinHeight = 150f;

        public static void AddEntry(GameObject entry)
        {
            if (entry == null) return;
            _entries.Add(entry);
            OnAddEntry();
        }

        public static void AddEntries(List<GameObject> entries)
        {
            var validEntries = entries.Where(entry => entry != null);
            if (validEntries.Count() == 0) return;
            _entries.AddRange(validEntries);
            OnAddEntry();
        }

        private static void OnAddEntry()
        {
            if (_instance == null) {
                _instance = GetWindow<SetupOutfitWindow>(Localization.S("check_outfit_drop_window.title"));
                _instance.minSize = new Vector2(MinWidth, MinHeight);
            }
            _instance.Repaint();
        }

        public static void RemoveEntry(GameObject entry)
        {
            _entries.Remove(entry);
            OnRemoveEntry();
        }

        public static void RemoveEntries(List<GameObject> entries)
        {
            foreach (var entry in entries)
            {
                _entries.RemoveAll(e => e == entry);
            }
            OnRemoveEntry();
        }

        public static void RemoveAllEntriesAndClose()
        {
            _entries.Clear();
            if (_instance != null) _instance.Close();
        }

        private static void OnRemoveEntry()
        {
            if (_instance != null)
            {
                if (_entries.Count == 0) {
                    _instance.Close();
                }
                else {
                    _instance.Repaint();
                }
            }
        }

        void OnEnable()
        {
            Localization.OnLangChange += Repaint;
        }

        void OnDisable()
        {
            _entries.Clear();
            if (_instance == this) _instance = null;
            Localization.OnLangChange -= Repaint;
        }

        void OnGUI()
        {
            ProcessDestroyedEntries();

            LogoDisplay.DisplayLogo();
            DrawTitleBox();
            DrawEnrties();
            DrawButtons();
            DrawDisableDescription();
            Localization.ShowLanguageUI();
        }

        private void ProcessDestroyedEntries()
        {
            using var _ = ListPool<GameObject>.Get(out var toRemove);
            foreach (var entry in _entries)
            {
                if (entry == null) toRemove.Add(entry!);
            }
            if (toRemove.Count > 0) RemoveEntries(toRemove);
        }

        private void DrawTitleBox()
        {
            var titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField(Localization.S("check_outfit_drop_window.description"), titleStyle, GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(5);

            var lastRect = GUILayoutUtility.GetLastRect();
            var lineY = lastRect.yMax - 1;
            var lineRect = new Rect(lastRect.xMin, lineY, lastRect.width, 2);
            EditorGUI.DrawRect(lineRect, new Color(1f, 1f, 1f, 0.3f));
        }

        private void DrawEnrties()
        {
            foreach (var entry in _entries)
            {
                EditorGUILayout.ObjectField(entry, typeof(GameObject), true);
            }
        }

        private void DrawButtons()
        {
            using var _ = new EditorGUILayout.HorizontalScope();
            
            if (GUILayout.Button(Localization.S("check_outfit_drop_window.setup_button"), GUILayout.ExpandWidth(true)))
            {
                foreach (var entry in _entries)
                {
                    SetupOutfit.SetupOutfitUI(entry);
                }
                RemoveAllEntriesAndClose();
            }
            
            if (GUILayout.Button(Localization.S("check_outfit_drop_window.dismiss_button"), GUILayout.Width(position.width * 0.35f - 20)))
            {
                RemoveAllEntriesAndClose();
            }
            
        }

        private void DrawDisableDescription()
        {
            var descriptionStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                wordWrap = true,
            };
            EditorGUILayout.LabelField(Localization.S("check_outfit_drop_window.disable_description"), descriptionStyle);
        }
    }
} 