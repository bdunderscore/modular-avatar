using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(AvatarObjectReference))]
    internal class AvatarObjectReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (CustomGUI(position, property, label)) return;

            var xButtonSize = EditorStyles.miniButtonRight.CalcSize(new GUIContent("x"));
            var xButtonRect = new Rect(position.xMax - xButtonSize.x, position.y, xButtonSize.x, position.height);
            position = new Rect(position.x, position.y, position.width - xButtonSize.x, position.height);

            property = property.FindPropertyRelative(nameof(AvatarObjectReference.referencePath));

            position = EditorGUI.PrefixLabel(position, label);

            using (var scope = new ZeroIndentScope())
            {
                EditorGUI.LabelField(position,
                    string.IsNullOrEmpty(property.stringValue) ? "(null)" : property.stringValue);
            }
        }

        private bool CustomGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var color = GUI.contentColor;

            var targetObjectProp = property.FindPropertyRelative(nameof(AvatarObjectReference.targetObject));
            property = property.FindPropertyRelative(nameof(AvatarObjectReference.referencePath));

            try
            {
                var avatarTransform = findContainingAvatarTransform(property);
                if (avatarTransform == null) return false;
                
                bool isRoot = property.stringValue == AvatarObjectReference.AVATAR_ROOT;
                bool isNull = string.IsNullOrEmpty(property.stringValue);
                Transform target;
                if (isNull) target = null;
                else if (isRoot) target = avatarTransform;
                else target = avatarTransform.Find(property.stringValue);

                if (targetObjectProp.objectReferenceValue is GameObject go &&
                    (go.transform == avatarTransform || go.transform.IsChildOf(avatarTransform)))
                {
                    target = go.transform;
                    isNull = false;
                    isRoot = target == avatarTransform;
                }

                var labelRect = position;
                position = EditorGUI.PrefixLabel(position, label);
                labelRect.width = position.x - labelRect.x;

                var nullContent = GUIContent.none;

                using (var scope = new ZeroIndentScope())
                {
                    if (target != null || isNull)
                    {
                        EditorGUI.BeginChangeCheck();
                        var newTarget = EditorGUI.ObjectField(position, nullContent, target, typeof(Transform), true);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (newTarget == null)
                            {
                                property.stringValue = "";
                            }
                            else if (newTarget == avatarTransform)
                            {
                                property.stringValue = AvatarObjectReference.AVATAR_ROOT;
                            }
                            else
                            {
                                var relPath =
                                    RuntimeUtil.RelativePath(avatarTransform.gameObject, ((Transform) newTarget).gameObject);
                                if (relPath == null) return true;

                                property.stringValue = relPath;
                            }

                            targetObjectProp.objectReferenceValue = ((Transform)newTarget)?.gameObject;
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
                            }
                            else if (newTarget == avatarTransform)
                            {
                                property.stringValue = AvatarObjectReference.AVATAR_ROOT;
                            }
                            else
                            {
                                var relPath =
                                    RuntimeUtil.RelativePath(avatarTransform.gameObject, ((Transform) newTarget).gameObject);
                                if (relPath == null) return true;

                                property.stringValue = relPath;
                            }

                            targetObjectProp.objectReferenceValue = ((Transform)newTarget)?.gameObject;
                        }
                        else
                        {
                            GUI.contentColor = Color.red;
                            EditorGUI.LabelField(position, property.stringValue);
                        }
                    }

                    return true;
                }
            }
            finally
            {
                GUI.contentColor = color;
            }
        }

        private static Transform findContainingAvatarTransform(SerializedProperty property)
        {
            // Find containing object, and from that the avatar
            if (property.serializedObject == null) return null;

            Transform commonAvatar = null;
            var targets = property.serializedObject.targetObjects;
            for (int i = 0; i < targets.Length; i++)
            {
                var obj = targets[i] as Component;
                if (obj == null) return null;

                var transform = obj.transform;
                var avatar = RuntimeUtil.FindAvatarTransformInParents(transform);

                if (i == 0)
                {
                    if (avatar == null) return null;
                    commonAvatar = avatar;
                }
                else if (commonAvatar != avatar) return null;
            }

            return commonAvatar;
        }
    }
}