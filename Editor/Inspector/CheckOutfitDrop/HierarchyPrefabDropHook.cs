#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class HierarchyPrefabDropHook
    {
        public static event Action<List<GameObject>>? OnPrefabAssetDropped;
        public static event Action<List<GameObject>>? OnPrefabInstanceDropped;

        [InitializeOnLoadMethod]
        static void Init()
        {
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
        }
    
        private static DragAndDropVisualMode OnHierarchyDrop(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (perform && (OnPrefabAssetDropped != null || OnPrefabInstanceDropped != null))
            {
                var droppedObjects = DragAndDrop.objectReferences.OfType<GameObject>().ToList();
                if (droppedObjects.Count > 0) 
                {
                    if (OnPrefabAssetDropped != null)
                    {
                        ProcessPrefabAssets(droppedObjects, OnPrefabAssetDropped);
                    }
                    if (OnPrefabInstanceDropped != null)
                    {
                        ProcessPrefabInstancesInScene(droppedObjects, OnPrefabInstanceDropped);
                    }
                }
            }
            return DragAndDropVisualMode.None; // handler is used for hook only.
        }

        private static void ProcessPrefabAssets(IEnumerable<GameObject> droppedObjects, Action<List<GameObject>> onDropped)
        {
            var droppedPrefabAssets = droppedObjects
                .Where(PrefabUtility.IsPartOfPrefabAsset)
                .ToHashSet();
            
            if (droppedPrefabAssets.Count == 0) return;

            StartWatchingCreatedPrefabInstances(droppedPrefabAssets, onDropped);
        }

        private static void StartWatchingCreatedPrefabInstances(HashSet<GameObject> droppedPrefabAssets, Action<List<GameObject>> onDropped)
        {
            var remainingUpdates = 10;
            var isSubscribed = false;

            bool StopWatching()
            {
                if (!isSubscribed) return false;

                ObjectChangeEvents.changesPublished -= HandleObjectChangesPublished;
                EditorApplication.update -= TimeoutWatching;
                isSubscribed = false;
                return true;
            }

            void TimeoutWatching()
            {
                remainingUpdates--;
                if (remainingUpdates <= 0)
                {
                    StopWatching();
                }
            }

            void HandleObjectChangesPublished(ref ObjectChangeEventStream stream)
            {
                OnObjectChangesPublished(stream, droppedPrefabAssets, onDropped, StopWatching);
            }

            ObjectChangeEvents.changesPublished += HandleObjectChangesPublished;
            EditorApplication.update += TimeoutWatching;
            isSubscribed = true;
        }

        private static void OnObjectChangesPublished(
            ObjectChangeEventStream stream,
            HashSet<GameObject> droppedPrefabAssets,
            Action<List<GameObject>> onDropped,
            Func<bool> stopWatching)
        {
            var createdGameObjects = CollectCreatedGameObjects(stream);
            if (createdGameObjects.Count == 0) return;

            // Delay to run after Unity finishes instantiating and parenting the dropped prefab.
            EditorApplication.delayCall += () =>
            {
                var targets = ResolveDroppedPrefabInstances(createdGameObjects, droppedPrefabAssets);
                if (targets.Count == 0 || !stopWatching()) return;

                onDropped.Invoke(targets);
            };
        }

        private static List<GameObject> CollectCreatedGameObjects(ObjectChangeEventStream stream)
        {
            var createdGameObjects = new List<GameObject>();

            for (var i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy) continue;

                stream.GetCreateGameObjectHierarchyEvent(i, out var data);
                if (EditorUtility.InstanceIDToObject(data.instanceId) is not GameObject createdGameObject) continue;

                createdGameObjects.Add(createdGameObject);
            }

            return createdGameObjects;
        }

        private static List<GameObject> ResolveDroppedPrefabInstances(IEnumerable<GameObject> createdGameObjects, HashSet<GameObject> droppedPrefabAssets)
        {
            var targets = new HashSet<GameObject>();

            foreach (var createdGameObject in createdGameObjects)
            {
                var prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(createdGameObject);
                if (prefabInstanceRoot == null) continue;

                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstanceRoot);
                if (sourcePrefab != null && droppedPrefabAssets.Contains(sourcePrefab))
                {
                    targets.Add(prefabInstanceRoot);
                }
            }

            return targets.ToList();
        }

        private static void ProcessPrefabInstancesInScene(IEnumerable<GameObject> droppedObjects, Action<List<GameObject>> onDropped)
        {
            // PrefabUtility.IsPartOfPrefabInstance may return true if it is part of an prefab asset.
            // so use PrefabUtility.IsPartOfNonAssetPrefabInstance instead.
            var droppedPrefabInstances = droppedObjects
                .Where(PrefabUtility.IsPartOfNonAssetPrefabInstance)
                .ToList();

            if (droppedPrefabInstances.Count == 0) return;
            
            // Delay to run after parenting a dropped gameobject 
            EditorApplication.delayCall += () =>
            {
                onDropped.Invoke(droppedPrefabInstances);
            };
        }

    }
}
