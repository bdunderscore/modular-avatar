#region

using System.Linq;
using nadena.dev.ndmf.platform;
using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarPlatformFilter))]
    internal class PlatformFilterEditor : MAEditorBase
    {
        private SerializedProperty m_platform;
        private SerializedProperty m_excludePlatform;
        
        private void OnEnable()
        {
            m_platform = serializedObject.FindProperty(nameof(ModularAvatarPlatformFilter.m_platform));
            m_excludePlatform = serializedObject.FindProperty(nameof(ModularAvatarPlatformFilter.m_excludePlatform));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUI.BeginChangeCheck();
            
            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, new GUIContent(G("platform_filter.platform")), m_platform);

            var knownPlatforms = PlatformRegistry.PlatformProviders.Values.OrderBy(kv => kv.DisplayName).ToList();
            var currentIndex = knownPlatforms.FindIndex(p => p.QualifiedName == m_platform.stringValue);
            var platformNames = knownPlatforms.Select(p => new GUIContent(p.DisplayName)).ToArray();

            if (!m_platform.hasMultipleDifferentValues && (currentIndex >= 0 || m_platform.stringValue == ""))
            {
                // Show a dropdown to select a platform
                currentIndex = EditorGUI.Popup(rect, label, currentIndex, platformNames.ToArray());
                
                if (currentIndex >= 0)
                {
                    m_platform.stringValue = knownPlatforms[currentIndex].QualifiedName;
                }
            }
            else
            {
                EditorGUI.showMixedValue = m_platform.hasMultipleDifferentValues;
                // Show a text field to enter a platform name, with a button to open a dropdown.
                
                var btnRect = new Rect(rect.xMax - rect.height, rect.y, rect.height, rect.height);
                rect = new Rect(rect.x, rect.y, rect.width - rect.height, rect.height);

                m_platform.stringValue = EditorGUI.TextField(rect, label, m_platform.stringValue);
                if (GUI.Button(btnRect, new GUIContent(), (GUIStyle)"DropDownButton"))
                {
                    EditorUtility.DisplayCustomMenu(
                        btnRect,
                        platformNames,
                        -1, 
                        (userData, options, selected) =>
                        {
                            if (selected >= 0 && selected < knownPlatforms.Count)
                            {
                                m_platform.stringValue = knownPlatforms[selected].QualifiedName;
                                serializedObject.ApplyModifiedProperties();
                            }
                        },
                        null,
                        false
                    );
                }
            }
            EditorGUI.EndProperty();
            
            var excludeRect = EditorGUILayout.GetControlRect();

            EditorGUI.BeginProperty(excludeRect, new GUIContent(), m_excludePlatform);
            var leftRect = new Rect(excludeRect.x, excludeRect.y, excludeRect.width / 2, excludeRect.height);
            var rightRect = new Rect(excludeRect.x + excludeRect.width / 2, excludeRect.y, excludeRect.width / 2, excludeRect.height);
            
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = m_excludePlatform.hasMultipleDifferentValues;

            var tmp = EditorGUI.ToggleLeft(leftRect, G("platform_filter.exclude"), m_excludePlatform.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                m_excludePlatform.boolValue = tmp;
            }

            EditorGUI.BeginChangeCheck();
            tmp = EditorGUI.ToggleLeft(rightRect, G("platform_filter.include"), !m_excludePlatform.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                m_excludePlatform.boolValue = !tmp;
            }
            EditorGUI.EndProperty();
            
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            ShowLanguageUI();
        }

    }
}
