using UnityEditor;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(AvatarObjectReference))]
    public class AvatarObjectReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!CustomGUI(position, property, label))
            {
                var xButtonSize = EditorStyles.miniButtonRight.CalcSize(new GUIContent("x"));
                var xButtonRect = new Rect(position.xMax - xButtonSize.x, position.y, xButtonSize.x, position.height);
                position = new Rect(position.x, position.y, position.width - xButtonSize.x, position.height);

                var isNull = property.FindPropertyRelative(nameof(AvatarObjectReference.isNull));
                property = property.FindPropertyRelative(nameof(AvatarObjectReference.referencePath));

                position = EditorGUI.PrefixLabel(position, label);

                EditorGUI.LabelField(position, isNull.boolValue ? "(null)" : property.stringValue);
            }
        }

        private bool CustomGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var indentLevel = EditorGUI.indentLevel;
            var color = GUI.contentColor;

            var isNull = property.FindPropertyRelative(nameof(AvatarObjectReference.isNull));
            property = property.FindPropertyRelative(nameof(AvatarObjectReference.referencePath));

            try
            {
                // Find containing object, and from that the avatar
                if (property.serializedObject == null || property.serializedObject.targetObjects.Length != 1)
                    return false;

                var obj = property.serializedObject.targetObject as Component;
                if (obj == null) return false;

                var transform = obj.transform;
                var avatar = RuntimeUtil.FindAvatarInParents(transform);
                if (avatar == null) return false;

                var target = isNull.boolValue ? null : avatar.transform.Find(property.stringValue);

                var labelRect = position;
                position = EditorGUI.PrefixLabel(position, label);
                labelRect.width = position.x - labelRect.x;

                var nullContent = GUIContent.none;

                if (target != null || isNull.boolValue)
                {
                    EditorGUI.BeginChangeCheck();
                    var newTarget = EditorGUI.ObjectField(position, nullContent, target, typeof(Transform), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (newTarget == null)
                        {
                            property.stringValue = "";
                            isNull.boolValue = true;
                        }
                        else
                        {
                            var relPath =
                                RuntimeUtil.RelativePath(avatar.gameObject, ((Transform) newTarget).gameObject);
                            if (relPath == null) return true;

                            property.stringValue = relPath;
                            isNull.boolValue = false;
                        }
                    }
                }
                else
                {
                    // For some reason, this color change retroactively affects the prefix label above, so draw our own
                    // label as well (we still want the prefix label for highlights, etc).
                    EditorGUI.LabelField(labelRect, label);

                    GUI.contentColor = new Color(0, 0, 0, 0);
                    EditorGUI.BeginChangeCheck();
                    var newTarget = EditorGUI.ObjectField(position, nullContent, target, typeof(Transform), true);
                    GUI.contentColor = color;

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (newTarget == null)
                        {
                            property.stringValue = "";
                            isNull.boolValue = true;
                        }
                        else
                        {
                            var relPath =
                                RuntimeUtil.RelativePath(avatar.gameObject, ((Transform) newTarget).gameObject);
                            if (relPath == null) return true;

                            property.stringValue = relPath;
                            isNull.boolValue = false;
                        }
                    }
                    else
                    {
                        GUI.contentColor = Color.red;
                        EditorGUI.LabelField(position, property.stringValue);
                    }
                }

                return true;
            }
            finally
            {
                GUI.contentColor = color;
                EditorGUI.indentLevel = indentLevel;
            }
        }
    }
}