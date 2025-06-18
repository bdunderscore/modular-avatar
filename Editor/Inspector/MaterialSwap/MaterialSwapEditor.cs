#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarMaterialSwap))]
    internal class MaterialSwapEditor : MAEditorBase
    {
        [SerializeField]
        private StyleSheet uss;

        [SerializeField]
        private VisualTreeAsset uxml;

        private DragAndDropManipulator _dragAndDropManipulator;

        private Dictionary<string, Dictionary<UnityEngine.Object, HashSet<Action<int>>>> _matNameToNameCountCallbacks = new();

        internal IDisposable RegisterMatNameCallback(string name, UnityEngine.Object target, Action<int> callback)
        {
            if (!_matNameToNameCountCallbacks.TryGetValue(name, out var obj_to_cb_set))
            {
                _matNameToNameCountCallbacks[name] = obj_to_cb_set = new();
            }

            if (!obj_to_cb_set.TryGetValue(target, out var cb_set))
            {
                obj_to_cb_set[target] = cb_set = new();
            }
            
            cb_set.Add(callback);
            var count = obj_to_cb_set.Count;
            foreach (var group in obj_to_cb_set)
            {
                foreach (var cb in group.Value)
                {
                    cb(count);
                }
            }

            return new Deregister(this, name, target, callback);
        }
        
        private class Deregister : IDisposable
        {
            private readonly MaterialSwapEditor _editor;
            private readonly string _name;
            private readonly UnityEngine.Object _target;
            private readonly Action<int> _callback;

            public Deregister(MaterialSwapEditor editor, string name, UnityEngine.Object target, Action<int> callback)
            {
                _editor = editor;
                _name = name;
                _target = target;
                _callback = callback;
            }

            public void Dispose()
            {
                if (!_editor._matNameToNameCountCallbacks.TryGetValue(_name, out var obj_to_cb_set)) return;
                if (!obj_to_cb_set.TryGetValue(_target, out var cb_set)) return;

                cb_set.Remove(_callback);
                if (cb_set.Count == 0)
                {
                    obj_to_cb_set.Remove(_target);
                }

                if (obj_to_cb_set.Count == 0)
                {
                    _editor._matNameToNameCountCallbacks.Remove(_name);
                }
                else
                {
                    foreach (var group in obj_to_cb_set)
                    {
                        foreach (var cb in group.Value)
                        {
                            cb(obj_to_cb_set.Count);
                        }
                    }
                }
            }
        }
        
        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = uxml.CloneTree();
            Localization.UI.Localize(root);
            root.styleSheets.Add(uss);

            root.Bind(serializedObject);

            ROSimulatorButton.BindRefObject(root, target);

            var listView = root.Q<ListView>("Swaps");

            listView.showBoundCollectionSize = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            listView.itemsAdded += OnItemsAdded;
            listView.makeItem = () => new MatSwapEditor(this);
            listView.bindItem = (e, i) =>
            {
                var item = (MatSwapEditor)e;
                item.BindProperty(serializedObject.FindProperty(nameof(ModularAvatarMaterialSwap.m_swaps)).GetArrayElementAtIndex(i));
            };

            _dragAndDropManipulator = new DragAndDropManipulator(root.Q("group-box"), target as ModularAvatarMaterialSwap);

            return root;
        }

        private void OnItemsAdded(IEnumerable<int> obj)
        {
            foreach (var index in obj)
            {
                var item = serializedObject.FindProperty(nameof(ModularAvatarMaterialSwap.m_swaps)).GetArrayElementAtIndex(index);
                
                // Clear the initial material entries
                var p_from = item.FindPropertyRelative(nameof(MatSwap.From));
                var p_to = item.FindPropertyRelative(nameof(MatSwap.To));

                p_from.objectReferenceValue = null;
                p_to.objectReferenceValue = null;
            }
            
            // We don't need to support undo, since this is a new entry.
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as ModularAvatarMaterialSwap;
        }

        private class DragAndDropManipulator : DragAndDropManipulator<ModularAvatarMaterialSwap, Material>
        {
            public DragAndDropManipulator(VisualElement targetElement, ModularAvatarMaterialSwap targetComponent)
                : base(targetElement, targetComponent)
            {
            }

            protected override IEnumerable<Material> FilterObjects(IEnumerable<Material> materials)
            {
                return materials.Where(x => TargetComponent.Swaps.All(y => x != y.From));
            }

            protected override void AddObjects(IEnumerable<Material> materials)
            {
                Undo.RecordObject(TargetComponent, "Add MatSwap");

                foreach (var material in materials)
                {
                    TargetComponent.Swaps.Add(new() { From = material });
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }
    }
}
