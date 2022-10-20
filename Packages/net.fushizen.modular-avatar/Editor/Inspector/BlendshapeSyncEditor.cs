using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarBlendshapeSync))]
    [CanEditMultipleObjects]
    internal class BlendshapeSyncEditor : Editor
    {
        private static FieldInfo f_m_SerializedObject;
        private BlendshapeSelectWindow _window;
        private ReorderableList _list;
        private SerializedProperty _bindings;

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

        private void OnDestroy()
        {
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

        private void DrawHeader(Rect rect)
        {
            var leftHalf = rect;
            leftHalf.width /= 2;

            var rightHalf = rect;
            rightHalf.width /= 2;
            rightHalf.x += rightHalf.width;

            EditorGUI.LabelField(leftHalf, "Mesh");
            EditorGUI.LabelField(rightHalf, "Blendshape");
        }

        private void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            rect.height -= 2;
            rect.y += 1;

            var leftHalf = rect;
            leftHalf.width /= 2;
            leftHalf.width -= 12;

            var rightHalf = rect;
            rightHalf.width /= 2;
            rightHalf.x += rightHalf.width;

            var item = _bindings.GetArrayElementAtIndex(index);
            var mesh = item.FindPropertyRelative(nameof(BlendshapeBinding.ReferenceMesh));
            var blendshape = item.FindPropertyRelative(nameof(BlendshapeBinding.Blendshape));

            using (var scope = new ZeroIndentScope())
            {
                EditorGUI.PropertyField(leftHalf, mesh, GUIContent.none);
                EditorGUI.PropertyField(rightHalf, blendshape, GUIContent.none);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void OpenAddWindow()
        {
            if (_window != null) DestroyImmediate(_window);
            _window = ScriptableObject.CreateInstance<BlendshapeSelectWindow>();
            _window.AvatarRoot = RuntimeUtil.FindAvatarInParents(((ModularAvatarBlendshapeSync) target).transform)
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