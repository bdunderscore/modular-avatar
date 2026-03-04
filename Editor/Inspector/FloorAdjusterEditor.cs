using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarFloorAdjuster))]
    internal class FloorAdjusterEditor : MAEditorBase
    {
        protected override void OnInnerInspectorGUI()
        {
            // no UI, just the logo
        }
    }
}