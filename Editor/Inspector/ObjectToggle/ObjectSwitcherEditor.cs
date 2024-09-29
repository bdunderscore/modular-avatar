#region

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomEditor(typeof(ModularAvatarObjectToggle))]
    public class ObjectSwitcherEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

        private DragAndDropManipulator _dragAndDropManipulator;

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

            var listView = root.Q<ListView>("Shapes");
            _dragAndDropManipulator = new DragAndDropManipulator(listView)
            {
                TargetComponent = target as ModularAvatarObjectToggle
            };

            listView.showBoundCollectionSize = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            return root;
        }

        private void OnEnable()
        {
            if (_dragAndDropManipulator != null)
                _dragAndDropManipulator.TargetComponent = target as ModularAvatarObjectToggle;
        }

        private class DragAndDropManipulator : PointerManipulator
        {
            public ModularAvatarObjectToggle TargetComponent;
            private GameObject[] _nowDragging = Array.Empty<GameObject>();
            private Transform _avatarRoot;

            private readonly VisualElement _parentElem;

            public DragAndDropManipulator(VisualElement target)
            {
                this.target = target;
                _parentElem = target.parent;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<DragEnterEvent>(OnDragEnter);
                target.RegisterCallback<DragLeaveEvent>(OnDragLeave);
                target.RegisterCallback<DragPerformEvent>(OnDragPerform);
                target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<DragEnterEvent>(OnDragEnter);
                target.UnregisterCallback<DragLeaveEvent>(OnDragLeave);
                target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
                target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            }


            private void OnDragEnter(DragEnterEvent evt)
            {
                if (TargetComponent == null) return;

                _avatarRoot = RuntimeUtil.FindAvatarTransformInParents(TargetComponent.transform);
                if (_avatarRoot == null) return;

                _nowDragging = DragAndDrop.objectReferences.OfType<GameObject>()
                    .Where(o => RuntimeUtil.FindAvatarTransformInParents(o.transform) == _avatarRoot)
                    .ToArray();

                if (_nowDragging.Length > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                    _parentElem.AddToClassList("drop-area--drag-active");
                }
            }

            private void OnDragUpdate(DragUpdatedEvent _)
            {
                if (_nowDragging.Length > 0) DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            }

            private void OnDragLeave(DragLeaveEvent evt)
            {
                _nowDragging = Array.Empty<GameObject>();
                _parentElem.RemoveFromClassList("drop-area--drag-active");
            }

            private void OnDragPerform(DragPerformEvent evt)
            {
                if (_nowDragging.Length > 0 && TargetComponent != null && _avatarRoot != null)
                {
                    var knownObjs = TargetComponent.Objects.Select(o => o.Object.Get(TargetComponent)).ToHashSet();

                    Undo.RecordObject(TargetComponent, "Add Toggled Objects");
                    foreach (var obj in _nowDragging)
                    {
                        if (knownObjs.Contains(obj)) continue;

                        var aor = new AvatarObjectReference();
                        aor.Set(obj);

                        var toggledObject = new ToggledObject { Object = aor, Active = !obj.activeSelf };
                        TargetComponent.Objects.Add(toggledObject);
                    }

                    EditorUtility.SetDirty(TargetComponent);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(TargetComponent);
                }

                _nowDragging = Array.Empty<GameObject>();
                _parentElem.RemoveFromClassList("drop-area--drag-active");
            }
        }
    }
}