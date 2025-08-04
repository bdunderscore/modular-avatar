using UnityEditor;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class UIElementsUtil
    {
        public static void UpdateWhileAttached(this VisualElement element,
            EditorApplication.CallbackFunction updateAction)
        {
            var isAttached = false;

            element.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (isAttached) return;
                isAttached = true;

                EditorApplication.update += updateAction;
            });

            element.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (!isAttached) return;
                isAttached = false;

                EditorApplication.update -= updateAction;
            });
        }
    }
}