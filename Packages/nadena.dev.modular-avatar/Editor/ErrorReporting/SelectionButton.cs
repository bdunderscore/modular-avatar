using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.editor.ErrorReporting
{
    internal class SelectionButton : Box
    {
        private UnityEngine.Object target;

        internal SelectionButton(string typeName, UnityEngine.Object target)
        {
            this.target = target;

            AddToClassList("selection-button");

            var tex = EditorGUIUtility.FindTexture("d_Search Icon");
            var icon = new Image {image = tex};
            Add(icon);

            var button = new Button(() =>
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            });

            //button.Add(new Label("[" + typeName + "] " + target.name));
            button.text = "[" + typeName + "] " + target.name;
            Add(button);
        }
    }
}