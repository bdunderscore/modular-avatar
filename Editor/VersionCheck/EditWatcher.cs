#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.editor.version
{
    internal sealed class EditWatcher : IDisposable
    {
        internal static EditWatcher Instance { get; } = new();

        public event Action<Object>? Changed;
        public event Action<GameObject>? Created;

        public EditWatcher()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        public void Dispose()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
        }

        private void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            var changed = new HashSet<Object>();
            var created = new HashSet<Object>();

            for (var i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    {
                        Object changedObject = null;
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(
                            i, out var changeEvent);

#if UNITY_6000_5_OR_NEWER
                        changedObject =
                            EditorUtility.EntityIdToObject(changeEvent.entityId);
#else
                        changedObject =
                            EditorUtility.InstanceIDToObject(changeEvent.instanceId);
#endif
                        changed.Add(changedObject);
                        break;
                    }
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    {
                        Object createdObject = null;
                        stream.GetCreateGameObjectHierarchyEvent(i, out var createEvent);

#if UNITY_6000_5_OR_NEWER
                        createdObject =
                            EditorUtility.EntityIdToObject(createEvent.entityId);
#else
                        createdObject =
                            EditorUtility.InstanceIDToObject(createEvent.instanceId);
#endif
                        created.Add(createdObject);
                        break;
                    }
                }
            }

            foreach (var target in created)
            {
                if (target is GameObject go) Created?.Invoke(go);
            }

            foreach (var target in changed)
            {
                Changed?.Invoke(target);
            }
        }
    }
}