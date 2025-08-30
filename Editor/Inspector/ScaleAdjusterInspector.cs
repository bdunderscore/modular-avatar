using System.Linq;
using UnityEditor;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarScaleAdjuster))]
    [CanEditMultipleObjects]
    internal class ScaleAdjusterInspector : MAEditorBase
    {
        private SerializedProperty _scale;

        protected void OnEnable()
        {
            _scale = serializedObject.FindProperty("m_Scale");
        }

        protected override void OnInnerInspectorGUI()
        {
            var scalers = targets.OfType<ModularAvatarScaleAdjuster>()
                .Select(s => new ScaleAdjusterTool.AdjusterScaler(s)).ToList();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_scale, G("scale_adjuster.scale"));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var s in scalers)
                {
                    s.Scale = _scale.vector3Value;
                }
            }

            ScaleAdjusterTool.AdjustChildPositions = EditorGUILayout.Toggle(G("scale_adjuster.adjust_children"),
                ScaleAdjusterTool.AdjustChildPositions);

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }
    }
}