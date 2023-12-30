#if MA_VRM0 || MA_VRM1

using nadena.dev.modular_avatar.core.vrm;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    [CustomPropertyDrawer(typeof(ModularAvatarMergeVRMFirstPerson.RendererFirstPersonFlags))]
    internal class RendererFirstPersonFlagsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var rendererProp = property.FindPropertyRelative(nameof(ModularAvatarMergeVRMFirstPerson.RendererFirstPersonFlags.renderer));
            var flagProp = property.FindPropertyRelative(nameof(ModularAvatarMergeVRMFirstPerson.RendererFirstPersonFlags.firstPersonFlag));

            const float rightSideWidth = 140.0f;

            var leftSide = position;
            leftSide.xMax -= rightSideWidth;
            EditorGUI.PropertyField(leftSide, rendererProp, GUIContent.none);

            var rightSide = position;
            rightSide.xMin = rightSide.xMax - rightSideWidth;
            EditorGUI.PropertyField(rightSide, flagProp, GUIContent.none);
        }
    }
}

#endif