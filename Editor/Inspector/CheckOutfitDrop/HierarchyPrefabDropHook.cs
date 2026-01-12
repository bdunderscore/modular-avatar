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
                var droppedObjects = DragAndDrop.objectReferences.OfType<GameObject>();
                if (droppedObjects.Count() > 0) 
                {
                    var dropDestinationParent = GetDropDestinationParent(dropTargetInstanceID, dropMode, parentForDraggedObjects);

                    if (OnPrefabAssetDropped != null)
                    {
                        ProcessPrefabAssets(droppedObjects, dropDestinationParent, OnPrefabAssetDropped);
                    }
                    if (OnPrefabInstanceDropped != null)
                    {
                        ProcessPrefabInstancesInScene(droppedObjects, OnPrefabInstanceDropped);
                    }
                }
            }
            return DragAndDropVisualMode.None; // handler is used for hook only.
        }

        private static Transform? GetDropDestinationParent(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects)
        {
            Transform? dropDestinationParent;

            if (parentForDraggedObjects == null)
            {
                var dropTarget = EditorUtility.InstanceIDToObject(dropTargetInstanceID) as GameObject;
                if (dropTarget == null) return null;

                dropDestinationParent = dropMode switch
                {
                    HierarchyDropFlags.DropUpon => dropTarget.transform,
                    HierarchyDropFlags.DropBetween or HierarchyDropFlags.DropAbove => dropTarget.transform.parent,
                    HierarchyDropFlags.DropAfterParent => dropTarget.transform.parent,
                    _ => null,
                };
            }
            else // parentForDraggedObjects may be set when in Prefab mode.
            {
                dropDestinationParent = parentForDraggedObjects;
            }

            return dropDestinationParent;
        }

        private static void ProcessPrefabAssets(IEnumerable<GameObject> droppedObjects, Transform? dropDestinationParent, Action<List<GameObject>> onDropped)
        {
            var droppedPrefabAssets = droppedObjects
                .Where(PrefabUtility.IsPartOfPrefabAsset)
                .ToHashSet();
            
            if (droppedPrefabAssets.Count == 0) return;

            // Get instanciaed prefab instances by comparing GameObjects in the scene between frames.

            var beforeSceneGameObjects = CollectSceneGameObjects(dropDestinationParent);
            // Delay to run after instanciating prefab and parenting a dropped gameobject
            EditorApplication.delayCall += () =>
            {
                var targets = new List<GameObject>();

                var afterSceneGameObjects = CollectSceneGameObjects(dropDestinationParent);
                afterSceneGameObjects.RemoveWhere(beforeSceneGameObjects.Contains);
                foreach (var newSceneGameObject in afterSceneGameObjects)
                {
                    var parentPrefab = PrefabUtility.GetCorrespondingObjectFromSource(newSceneGameObject);
                    if (parentPrefab != null && droppedPrefabAssets.Contains(parentPrefab))
                    {
                        targets.Add(newSceneGameObject);
                    }
                }
                
                onDropped.Invoke(targets);
            };

            // if the destination is null (unknown or the root), all game objects in scenes are searched.
            // If the destination is known, only the direct children are searched for optimazation.
            static HashSet<GameObject> CollectSceneGameObjects(Transform? parent)
            {
                if (parent != null)
                {
                    var directChildren = new HashSet<GameObject>();
                    foreach (Transform child in parent)
                    {
                        directChildren.Add(child.gameObject);
                    }
                    return directChildren;
                }
                else
                {
                    var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                    return allGameObjects.ToHashSet();
                }
            }
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