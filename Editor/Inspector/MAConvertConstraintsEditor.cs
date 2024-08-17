using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarConvertConstraints))]
    [CanEditMultipleObjects]
    internal class MAConvertConstraintsEditor : MAEditorBase
    {
        protected override void OnInnerInspectorGUI()
        {
            // no UI
        }
    }
}