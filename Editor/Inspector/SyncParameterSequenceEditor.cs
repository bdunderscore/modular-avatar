using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarSyncParameterSequence))]
    [CanEditMultipleObjects]
    public class SyncParameterSequenceEditor : MAEditorBase
    {
        private SerializedProperty _p_platform;
        private SerializedProperty _p_parameters;

        private void OnEnable()
        {
            _p_platform = serializedObject.FindProperty(nameof(ModularAvatarSyncParameterSequence.PrimaryPlatform));
            _p_parameters = serializedObject.FindProperty(nameof(ModularAvatarSyncParameterSequence.Parameters));
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

#if MA_VRCSDK3_AVATARS
            var disable = false;
#else
            bool disable = true;
#endif

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (disable)
                // ReSharper disable HeuristicUnreachableCode
            {
                EditorGUILayout.HelpBox(S("general.vrcsdk-required"), MessageType.Warning);
            }
            // ReSharper restore HeuristicUnreachableCode

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            using (new EditorGUI.DisabledGroupScope(disable))
            {
                EditorGUILayout.PropertyField(_p_platform, G("sync-param-sequence.platform"));
                GUILayout.BeginHorizontal();

                var label = G("sync-param-sequence.parameters");
                var sizeCalc = EditorStyles.objectField.CalcSize(label);
                EditorGUILayout.PropertyField(_p_parameters, label);

                if (GUILayout.Button(G("sync-param-sequence.create-asset"),
                        GUILayout.ExpandWidth(false),
                        GUILayout.Height(sizeCalc.y)
                    ))
                {
                    CreateParameterAsset();
                }

                GUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            ShowLanguageUI();
        }

        private void CreateParameterAsset()
        {
#if MA_VRCSDK3_AVATARS
            Transform avatarRoot = null;
            if (targets.Length == 1)
            {
                avatarRoot =
                    RuntimeUtil.FindAvatarTransformInParents(((ModularAvatarSyncParameterSequence)target).transform);
            }

            var assetName = "Avatar";
            if (avatarRoot != null) assetName = avatarRoot.gameObject.name;

            assetName += " SyncedParams";

            var file = EditorUtility.SaveFilePanelInProject("Create new parameter asset", assetName, "asset",
                "Create a new parameter asset");

            var obj = CreateInstance<VRCExpressionParameters>();
            obj.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
            obj.isEmpty = true;

            AssetDatabase.CreateAsset(obj, file);
            Undo.RegisterCreatedObjectUndo(obj, "Create parameter asset");

            _p_parameters.objectReferenceValue = obj;
            serializedObject.ApplyModifiedProperties();
#endif
        }
    }
}