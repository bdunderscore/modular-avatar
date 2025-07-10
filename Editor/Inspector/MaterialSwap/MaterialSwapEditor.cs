#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(QuickSwapMode))]
    internal class QuickSwapModeDrawer : EnumDrawer<QuickSwapMode>
    {
        protected override string localizationPrefix => "reactive_object.material-swap.quick_swap_mode";
    }

    [CustomEditor(typeof(ModularAvatarMaterialSwap))]
    internal class MaterialSwapEditor : MAEditorBase
    {
        [SerializeField]
        private StyleSheet uss;

        [SerializeField]
        private VisualTreeAsset uxml;

        private DragAndDropManipulator _dragAndDropManipulator;

        private Dictionary<string, Dictionary<UnityEngine.Object, HashSet<Action<string>>>> _matNameToUniquePathCallbacks = new();

        private bool _isSiblingMode;
        private bool isSiblingMode
        {
            get => _isSiblingMode;
            set
            {
                if (_isSiblingMode == value) return;
                
                Debug.Log("Sibling mode changed to " + value);
                
                _isSiblingMode = value;

                foreach (var pair in _matNameToUniquePathCallbacks)
                {
                    CallUniquePathCallback(pair.Value);
                }
            }
        }

        internal IDisposable RegisterUniquePathCallback(string name, UnityEngine.Object target, Action<string> callback)
        {
            if (!_matNameToUniquePathCallbacks.TryGetValue(name, out var obj_to_cb_set))
            {
                _matNameToUniquePathCallbacks[name] = obj_to_cb_set = new();
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
                var unique_path = obj_to_folder_path.Count <= 1 && !_isSiblingMode ? null : GetUniquePath(obj_to_folder_path.Values, folder_path);
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
            private readonly MaterialSwapEditor _editor;
            private readonly string _name;
            private readonly UnityEngine.Object _target;
            private readonly Action<string> _callback;

            public Deregister(MaterialSwapEditor editor, string name, UnityEngine.Object target, Action<string> callback)
            {
                _editor = editor;
                _name = name;
                _target = target;
                _callback = callback;
            }

            public void Dispose()
            {
                if (!_editor._matNameToUniquePathCallbacks.TryGetValue(_name, out var obj_to_cb_set)) return;
                if (!obj_to_cb_set.TryGetValue(_target, out var cb_set)) return;

                cb_set.Remove(_callback);
                if (cb_set.Count == 0)
                {
                    obj_to_cb_set.Remove(_target);
                }

                if (obj_to_cb_set.Count == 0)
                {
                    _editor._matNameToUniquePathCallbacks.Remove(_name);
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
            listView.makeItem = () => new MatSwapEditor(this);
            listView.bindItem = (e, i) =>
            {
                var item = (MatSwapEditor)e;
                item.BindProperty(serializedObject.FindProperty(nameof(ModularAvatarMaterialSwap.m_swaps)).GetArrayElementAtIndex(i));
            };

            _dragAndDropManipulator = new DragAndDropManipulator(root.Q("group-box"), target as ModularAvatarMaterialSwap);

            var qsMode = root.Q<EnumField>("quick-swap-mode-field");
            qsMode.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                bool isNone = nameof(QuickSwapMode.None) == evt.newValue;
                isSiblingMode = nameof(QuickSwapMode.SiblingDirectory) == evt.newValue.Replace(" ", "");

                if (isNone)
                {
                    root.RemoveFromClassList("quick-swap-enable");
                } else 
                {
                    root.AddToClassList("quick-swap-enable");
                }
            });
            
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
                    TargetComponent.Swaps.Add(new() { From = material, To = material });
                }

                EditorUtility.SetDirty(TargetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
            }
        }
    }
}
