using System;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarWorldFixedObject))]
    internal class WorldFixedObjectEditor : MAEditorBase
    {
        protected override void OnInnerInspectorGUI()
        {
#if UNITY_ANDROID
            EditorGUILayout.HelpBox(Localization.S("worldfixed.quest"), MessageType.Warning);

#else
            EditorGUILayout.HelpBox(Localization.S("worldfixed.normal"), MessageType.Info);
#endif

            Localization.ShowLanguageUI();
        }
    }
}