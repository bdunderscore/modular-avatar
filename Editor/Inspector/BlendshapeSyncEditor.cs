using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarBlendshapeSync))]
    [CanEditMultipleObjects]
    internal class BlendshapeSyncEditor : MAEditorBase
    {
        private static FieldInfo f_m_SerializedObject;
        private BlendshapeSelectWindow _window;
        private ReorderableList _list;
        private SerializedProperty _bindings;

        private Dictionary<Mesh, string[]> blendshapeNames = new Dictionary<Mesh, string[]>();

        static BlendshapeSyncEditor()
        {
            f_m_SerializedObject =
                typeof(Editor).GetField("m_SerializedObject", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        // Workaround unity bug: When we modify the number of array elements via the underlying objects, the serialized
        // object will throw exceptions trying to access the new element, even after calling Update() and recreating all
        // serialized properties. So force the serialized object to be recreated as a workaround.
        private void ClearSerializedObject()
        {
            f_m_SerializedObject.SetValue(this, null);
        }

        private void OnDisable()
        {
            if (_window != null) DestroyImmediate(_window);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_window != null) DestroyImmediate(_window);
        }

        private void OnEnable()
        {
            InitList();
        }

        private void InitList()
        {
            _bindings = serializedObject.FindProperty(nameof(ModularAvatarBlendshapeSync.Bindings));
            _list = new ReorderableList(serializedObject,
                _bindings,
                true, true, true, true
            );
            _list.drawHeaderCallback = DrawHeader;
            _list.drawElementCallback = DrawElement;
            _list.onAddCallback = list => OpenAddWindow();
            _list.elementHeight += 2;
        }

        private float elementWidth = 0;

        private void ComputeRects(
            Rect rect,
            out Rect meshFieldRect,
            out Rect baseShapeNameRect,
            out Rect targetShapeNameRect
        )
        {
            if (elementWidth > 1 && elementWidth < rect.width)
            {
                rect.x += rect.width - elementWidth;
                rect.width = elementWidth;
            }

            meshFieldRect = rect;
            meshFieldRect.width /= 3;

            baseShapeNameRect = rect;
            baseShapeNameRect.width /= 3;
            baseShapeNameRect.x = meshFieldRect.x + meshFieldRect.width;

            targetShapeNameRect = rect;
            targetShapeNameRect.width /= 3;
            targetShapeNameRect.x = baseShapeNameRect.x + baseShapeNameRect.width;

            meshFieldRect.width -= 12;
            baseShapeNameRect.width -= 12;
        }

        private void DrawHeader(Rect rect)
        {
            ComputeRects(rect, out var meshFieldRect, out var baseShapeNameRect, out var targetShapeNameRect);

            EditorGUI.LabelField(meshFieldRect, G("blendshape.mesh"));
            EditorGUI.LabelField(baseShapeNameRect, G("blendshape.source"));
            EditorGUI.LabelField(targetShapeNameRect, G("blendshape.target"));
        }

        private void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            rect.height -= 2;
            rect.y += 1;

            if (Math.Abs(elementWidth - rect.width) > 0.5f && rect.width > 1)
            {
                elementWidth = rect.width;
                Repaint();
            }

            ComputeRects(rect, out var meshFieldRect, out var baseShapeNameRect, out var targetShapeNameRect);

            var item = _bindings.GetArrayElementAtIndex(index);
            var mesh = item.FindPropertyRelative(nameof(BlendshapeBinding.ReferenceMesh));
            var sourceBlendshape = item.FindPropertyRelative(nameof(BlendshapeBinding.Blendshape));
            var localBlendshape = item.FindPropertyRelative(nameof(BlendshapeBinding.LocalBlendshape));

            using (var scope = new ZeroIndentScope())
            {
                EditorGUI.PropertyField(meshFieldRect, mesh, GUIContent.none);

                Mesh sourceMesh = null, localMesh = null;
                if (targets.Length == 1)
                {
                    var targetMeshObject = (target as ModularAvatarBlendshapeSync)
                        ?.Bindings[index]
                        .ReferenceMesh
                        ?.Get((Component) target);
                    if (targetMeshObject != null)
                    {
                        sourceMesh = targetMeshObject.GetComponent<SkinnedMeshRenderer>()
                            ?.sharedMesh;
                    }

                    localMesh =
                        (target as ModularAvatarBlendshapeSync)
                        ?.GetComponent<SkinnedMeshRenderer>()
                        ?.sharedMesh;
                }

                DrawBlendshapePopup(sourceMesh, baseShapeNameRect, sourceBlendshape);

                DrawBlendshapePopup(localMesh, targetShapeNameRect, localBlendshape, sourceBlendshape.stringValue);
            }
        }

        private void DrawBlendshapePopup(Mesh targetMesh, Rect rect, SerializedProperty prop,
            string defaultValue = null)
        {
            var style = new GUIStyle(EditorStyles.popup);

            style.fixedHeight = rect.height;

            if (targetMesh == null)
            {
                EditorGUI.PropertyField(rect, prop, GUIContent.none);
            }
            else
            {
                string[] selections = GetBlendshapeNames(targetMesh);

                int shapeIndex = Array.FindIndex(selections, s => s == prop.stringValue);

                EditorGUI.BeginChangeCheck();
                int newShapeIndex = EditorGUI.Popup(rect, shapeIndex, selections, style);
                if (EditorGUI.EndChangeCheck())
                {
                    prop.stringValue = selections[newShapeIndex];
                }
                else if (shapeIndex < 0)
                {
                    var toDisplay = prop.stringValue;
                    bool colorRed = true;

                    if (string.IsNullOrEmpty(toDisplay) && defaultValue != null)
                    {
                        toDisplay = defaultValue;
                        colorRed = Array.FindIndex(selections, s => s == toDisplay) < 0;
                    }

                    if (!colorRed)
                    {
                        UpdateAllStates(style, s => s.textColor = Color.Lerp(s.textColor, Color.clear, 0.2f));
                        style.fontStyle = FontStyle.Italic;
                    }
                    else
                    {
                        UpdateAllStates(style, s => s.textColor = Color.Lerp(s.textColor, Color.red, 0.85f));
                    }

                    GUI.Label(rect, toDisplay, style);
                }
            }
        }

        private static void UpdateAllStates(GUIStyle style, Action<GUIStyleState> action)
        {
            var state = style.normal;
            action(state);
            style.normal = state;

            state = style.hover;
            action(state);
            style.hover = state;

            state = style.active;
            action(state);
            style.active = state;

            state = style.focused;
            action(state);
            style.focused = state;

            state = style.onNormal;
            action(state);
            style.onNormal = state;

            state = style.onHover;
            action(state);
            style.onHover = state;

            state = style.onActive;
            action(state);
            style.onActive = state;

            state = style.onFocused;
            action(state);
            style.onFocused = state;
        }

        private string[] GetBlendshapeNames(Mesh targetMesh)
        {
            if (!blendshapeNames.TryGetValue(targetMesh, out var selections))
            {
                selections = new string[targetMesh.blendShapeCount];
                for (int i = 0; i < targetMesh.blendShapeCount; i++)
                {
                    selections[i] = targetMesh.GetBlendShapeName(i);
                }

                blendshapeNames[targetMesh] = selections;
            }

            return selections;
        }

        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            _list.DoLayoutList();

            ShowLanguageUI();

            serializedObject.ApplyModifiedProperties();
        }

        private void OpenAddWindow()
        {
            if (_window != null) DestroyImmediate(_window);
            _window = ScriptableObject.CreateInstance<BlendshapeSelectWindow>();
            _window.AvatarRoot = RuntimeUtil.FindAvatarTransformInParents(((ModularAvatarBlendshapeSync) target).transform)
                .gameObject;
            _window.OfferBinding += OfferBinding;
            _window.Show();
        }

        private void OfferBinding(BlendshapeBinding binding)
        {
            foreach (var obj in targets)
            {
                var sync = (ModularAvatarBlendshapeSync) obj;
                Undo.RecordObject(sync, "Adding blendshape binding");
                if (!sync.Bindings.Contains(binding)) sync.Bindings.Add(binding);
                EditorUtility.SetDirty(sync);
            }

            ClearSerializedObject();
            InitList();
        }
    }
}