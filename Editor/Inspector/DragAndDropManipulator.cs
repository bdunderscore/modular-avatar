using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal abstract class DragAndDropManipulator<TTargetComponent, TDragObject> : PointerManipulator
        where TTargetComponent : Component
        where TDragObject : Object
    {
        private const string DragActiveClassName = "drop-area--drag-active";

        public TTargetComponent TargetComponent { get; set; }
        protected Transform AvatarRoot => _avatarRoot;

        private Transform _avatarRoot;
        private TDragObject[] _draggingObjects = Array.Empty<TDragObject>();

        public DragAndDropManipulator(VisualElement targetElement, TTargetComponent targetComponent)
        {
            target = targetElement;
            TargetComponent = targetComponent;
        }

        protected sealed override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<DragEnterEvent>(OnDragEnter);
            target.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            target.RegisterCallback<DragExitedEvent>(OnDragExited);
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected sealed override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<DragEnterEvent>(OnDragEnter);
            target.UnregisterCallback<DragLeaveEvent>(OnDragLeave);
            target.UnregisterCallback<DragExitedEvent>(OnDragExited);
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private void OnDragEnter(DragEnterEvent _)
        {
            if (TargetComponent == null) return;

            _avatarRoot = RuntimeUtil.FindAvatarTransformInParents(TargetComponent.transform);
            if (_avatarRoot == null) return;

            _draggingObjects = FilterObjects(DragAndDrop.objectReferences.OfType<TDragObject>())
                .ToArray();
            if (_draggingObjects.Length == 0) return;

            target.AddToClassList(DragActiveClassName);
        }

        private void OnDragLeave(DragLeaveEvent _)
        {
            _draggingObjects = Array.Empty<TDragObject>();
            target.RemoveFromClassList(DragActiveClassName);
        }

        private void OnDragExited(DragExitedEvent _)
        {
            _draggingObjects = Array.Empty<TDragObject>();
            target.RemoveFromClassList(DragActiveClassName);
        }

        private void OnDragUpdated(DragUpdatedEvent _)
        {
            if (TargetComponent == null) return;
            if (_avatarRoot == null) return;
            if (_draggingObjects.Length == 0) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
        }

        private void OnDragPerform(DragPerformEvent _)
        {
            if (TargetComponent == null) return;
            if (_avatarRoot == null) return;
            if (_draggingObjects.Length == 0) return;

            AddObjects(_draggingObjects);
        }

        protected virtual IEnumerable<TDragObject> FilterObjects(IEnumerable<TDragObject> objects)
        {
            return objects;
        }

        protected abstract void AddObjects(IEnumerable<TDragObject> objects);
    }

    internal abstract class DragAndDropManipulator<T> : DragAndDropManipulator<T, GameObject>
        where T : Component, IHaveObjReferences
    {
        protected virtual bool AllowKnownObjects => true;

        public DragAndDropManipulator(VisualElement targetElement, T targetComponent)
            : base(targetElement, targetComponent) { }

        protected override IEnumerable<GameObject> FilterObjects(IEnumerable<GameObject> objects)
        {
            var knownObjects = TargetComponent.GetObjectReferences().Select(x => x.Get(TargetComponent)).ToHashSet();
            return objects
                .Where(x => AllowKnownObjects || !knownObjects.Contains(x))
                .Where(x => RuntimeUtil.FindAvatarTransformInParents(x.transform) == AvatarRoot)
                .Where(FilterGameObject);
        }

        protected override void AddObjects(IEnumerable<GameObject> objects)
        {
            AddObjectReferences(objects
                .Select(x =>
                {
                    var reference = new AvatarObjectReference();
                    reference.Set(x);
                    return reference;
                })
                .ToArray());
        }

        protected virtual bool FilterGameObject(GameObject obj)
        {
            return true;
        }

        protected abstract void AddObjectReferences(AvatarObjectReference[] references);
    }
}
