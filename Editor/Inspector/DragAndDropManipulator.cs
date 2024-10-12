using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal abstract class DragAndDropManipulator<T> : PointerManipulator where T : Component, IHaveObjReferences
    {
        private const string DragActiveClassName = "drop-area--drag-active";

        public T TargetComponent { get; set; }

        protected virtual bool AllowKnownObjects => true;

        private Transform _avatarRoot;
        private GameObject[] _draggingObjects = Array.Empty<GameObject>();

        public DragAndDropManipulator(VisualElement targetElement, T targetComponent)
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

            var knownObjects = TargetComponent.GetObjectReferences().Select(x => x.Get(TargetComponent)).ToHashSet();
            _draggingObjects = DragAndDrop.objectReferences.OfType<GameObject>()
                .Where(x => AllowKnownObjects || !knownObjects.Contains(x))
                .Where(x => RuntimeUtil.FindAvatarTransformInParents(x.transform) == _avatarRoot)
                .Where(FilterGameObject)
                .ToArray();
            if (_draggingObjects.Length == 0) return;

            target.AddToClassList(DragActiveClassName);
        }

        private void OnDragLeave(DragLeaveEvent _)
        {
            _draggingObjects = Array.Empty<GameObject>();
            target.RemoveFromClassList(DragActiveClassName);
        }

        private void OnDragExited(DragExitedEvent _)
        {
            _draggingObjects = Array.Empty<GameObject>();
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

            AddObjectReferences(_draggingObjects
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
