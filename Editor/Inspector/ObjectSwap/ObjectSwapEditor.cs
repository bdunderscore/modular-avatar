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
    internal abstract class ObjectSwapEditor<TObj, TObjSwap, TObjectSwap> : MAEditorBase
        where TObj : UnityEngine.Object
        where TObjSwap : IObjSwap<TObj>, new()
        where TObjectSwap : Component, IObjectSwap<TObj, TObjSwap>
    {
        [SerializeField]
        private StyleSheet uss;

        [SerializeField]
        private VisualTreeAsset uxml;

        private DragAndDropManipulator _dragAndDropManipulator;

        private Dictionary<string, Dictionary<UnityEngine.Object, HashSet<Action<string>>>> _objNameToUniquePathCallbacks = new();

        internal abstract IEnumerable<UnityEngine.Object> GetObjects(SerializedProperty property);

        internal IDisposable RegisterUniquePathCallback(string name, UnityEngine.Object target, Action<string> callback)
        {
            if (!_objNameToUniquePathCallbacks.TryGetValue(name, out var obj_to_cb_set))
            {
                _objNameToUniquePathCallbacks[name] = obj_to_cb_set = new();
            }

            if (!obj_to_cb_set.TryGetValue(target, out var cb_set))
            {
                obj_to_cb_set[target] = cb_set = new();
            }
            
            cb_set.Add(callback);
            CallUniquePathCallback(obj_to_cb_set);

            return new Deregister(this, name, target, callback);
        }

        private void CallUniquePathCallback(Dictionary<UnityEngine.Object, HashSet<Action<string>>> obj_to_cb_set)
        {
            var obj_to_folder_path = obj_to_cb_set.ToDictionary(x => x.Key, x => GetFolderPath(x.Key));
            foreach (var (obj, folder_path) in obj_to_folder_path)
            {
                var unique_path = obj_to_folder_path.Count <= 1 ? null : GetUniquePath(obj_to_folder_path.Values, folder_path);
                foreach (var cb in obj_to_cb_set[obj])
                {
                    cb(unique_path);
                }
            }
        }

        private string GetFolderPath(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return null;

            var splits = path.Split('/');
            return string.Join('/', splits[..^1]);
        }

        private string GetUniquePath(IReadOnlyCollection<string> paths, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Extract parent path segment from asset path
            var splits = path.Split('/');
            for (var i = 1; i <= splits.Length; i++)
            {
                var suffix = string.Join('/', splits.TakeLast(i));
                if (paths.All(p => p == null || p == path || !p.EndsWith(suffix)))
                {
                    return $"{suffix}/";
                }
            }
            return null;
        }
        
        private class Deregister : IDisposable
        {
            private readonly ObjectSwapEditor<TObj, TObjSwap, TObjectSwap> _editor;
            private readonly string _name;
            private readonly UnityEngine.Object _target;
            private readonly Action<string> _callback;

            public Deregister(ObjectSwapEditor<TObj, TObjSwap, TObjectSwap> editor, string name, UnityEngine.Object target, Action<string> callback)
            {
                _editor = editor;
                _name = name;
                _target = target;
                _callback = callback;
            }

            public void Dispose()
            {
                if (!_editor._objNameToUniquePathCallbacks.TryGetValue(_name, out var obj_to_cb_set)) return;
                if (!obj_to_cb_set.TryGetValue(_target, out var cb_set)) return;

                cb_set.Remove(_callback);
                if (cb_set.Count == 0)
                {
                    obj_to_cb_set.Remove(_target);
                }

                if (obj_to_cb_set.Count == 0)
                {
                    _editor._objNameToUniquePathCallbacks.Remove(_name);
                }
                else
                {
                    _editor.CallUniquePathCallback(obj_to_cb_set);
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
            listView.makeItem = () => new ObjSwapEditor<TObj, TObjSwap, TObjectSwap>(this);
            listView.bindItem = (e, i) =>
            {
                var item = (ObjSwapEditor<TObj, TObjSwap, TObjectSwap>)e;
                item.BindProperty(serializedObject.FindProperty("m_swaps").GetArrayElementAtIndex(i));
            };

            _dragAndDropManipulator = new(root.Q("group-box"), target as TObjectSwap);

            return root;
        }

        private void OnItemsAdded(IEnumerable<int> obj)
        {
            foreach (var index in obj)
            {
                var item = serializedObject.FindProperty("m_swaps").GetArrayElementAtIndex(index);
                
                // Clear the initial entries
                var p_from = item.FindPropertyRelative("From");
                var p_to = item.FindPropertyRelative("To");

                p_from.objectReferenceValue = null;
                p_to.objectReferenceValue = null;
            }
            
            // We don't need to support undo, since this is a new entry.
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as TObjectSwap;
        }

        private class DragAndDropManipulator : DragAndDropManipulator<TObjectSwap, TObj>
        {
            public DragAndDropManipulator(VisualElement targetElement, TObjectSwap targetComponent)
                : base(targetElement, targetComponent)
            {
            }

            protected override IEnumerable<TObj> FilterObjects(IEnumerable<TObj> objects)
            {
                return objects.Where(x => TargetComponent.Swaps.All(y => x != y.From));
            }

            protected override void AddObjects(IEnumerable<TObj> objects)
            {
                Undo.RecordObject(TargetComponent, "Add Swap Entry");

                foreach (var obj in objects)
                {
                    TargetComponent.Swaps.Add(new() { From = obj, To = obj });
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }
    }
}
