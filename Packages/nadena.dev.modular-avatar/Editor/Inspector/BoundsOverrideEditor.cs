/*
 * MIT License
 * 
 * Copyright (c) 2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarBoundsOverride))]
    [CanEditMultipleObjects]
    internal class BoundsOverrideEditor : MAEditorBase
    {
        private SerializedProperty _rootBoneTargetProperty;
        private SerializedProperty _boundsProperty;

        private void OnEnable()
        {
            _rootBoneTargetProperty = serializedObject.FindProperty(nameof(ModularAvatarBoundsOverride.rootBoneTarget));
            _boundsProperty = serializedObject.FindProperty(nameof(ModularAvatarBoundsOverride.bounds));
        }

        protected override void OnInnerInspectorGUI()
        {
            // TODO: 言語ファイル対応
            EditorGUILayout.HelpBox(S("bounds_override.help"), MessageType.Info);

            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                serializedObject.Update();

                EditorGUILayout.PropertyField(_rootBoneTargetProperty, G("bounds_override.root_bone"));
                EditorGUILayout.PropertyField(_boundsProperty, G("bounds_override.bounds"));

                if (changeCheckScope.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }

            Localization.ShowLanguageUI();
        }


        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGizmo(ModularAvatarBoundsOverride component, GizmoType gizmoType)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;

            Vector3 center = component.bounds.center;
            Vector3 size = component.bounds.size;
            try
            {
                Transform rootBone = component.rootBoneTarget.Get(component)?.transform;
                if (rootBone != null)
                {
                    Gizmos.matrix *= rootBone.localToWorldMatrix;
                }
            } catch (NullReferenceException e)
            {
                Console.WriteLine(e);
                component.rootBoneTarget.referencePath = null;
            }

            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = oldMatrix;
        }
    }
}