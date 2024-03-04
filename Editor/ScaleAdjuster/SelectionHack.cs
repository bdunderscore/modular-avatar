using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.ScaleAdjuster
{
    #if !UNITY_2022_3_OR_NEWER
    internal static class SelectionHack
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            Selection.selectionChanged += OnSelectionChanged;
            
        }

        static void OnSelectionChanged()
        {
            var gameObject = Selection.activeGameObject;
            if (gameObject != null && gameObject.GetComponent<ScaleAdjusterRenderer>() != null)
            {
                EditorApplication.delayCall += () =>
                {
                    Selection.activeGameObject = gameObject.transform.parent.gameObject;
                };
            }
        }
    }
    #endif
}