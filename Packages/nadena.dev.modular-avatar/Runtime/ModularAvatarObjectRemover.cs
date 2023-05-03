using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [ExecuteInEditMode]
    [AddComponentMenu("Modular Avatar/MA Object Remover")]
    public class ModularAvatarObjectRemover : AvatarTagComponent
    {
        public bool hideInHierarchy = true;
        public bool keepDisabled = true;
        public bool removePrefabComponents;
        public List<GameObject> objectsToRemove = new List<GameObject>();
        private List<GameObject> _lastObjectsToRemove;

        private void OnEnable()
        {
            objectsToRemove?.RemoveAll(x => !x);
            OnListChange();
        }

        private void Update()
        {
            if (Application.isPlaying) return;
            if (!keepDisabled) return;
            if (objectsToRemove == null) return;
            foreach (var toRemove in objectsToRemove) toRemove.SetActive(false);
        }
        
        private new void OnDestroy()
        {
            if (Application.isPlaying) return;
            hideInHierarchy = false;
            removePrefabComponents = false;
            Unhide();
            RestorePrefabComponents();
            objectsToRemove.Clear();
            _lastObjectsToRemove = null;
        }

        public void OnListChange()
        {
            objectsToRemove.RemoveAll(x => !x);
            if (Application.isPlaying) return;
            if (_lastObjectsToRemove != null)
            {
                foreach (var newValue in objectsToRemove) _lastObjectsToRemove.Remove(newValue);
                foreach (var removedFromList in _lastObjectsToRemove)
                {
                    if (!removedFromList) continue;
                    removedFromList.hideFlags &= ~HideFlags.HideInHierarchy;
                    foreach (var removedComponent in CollectComponentsToRestore(new List<RemovedComponent>(), removedFromList.transform))
                    {
                        removedComponent.Revert();
                    }
                }
            }

            _lastObjectsToRemove = new List<GameObject>(objectsToRemove);
            if (hideInHierarchy) Hide();
            if (removePrefabComponents) RemovePrefabComponents();
        }

        private void CollectComponentsToRemove(List<Component> list, Transform current)
        {
            list.AddRange(current.GetComponents(typeof(Component)).Where(PrefabUtility.IsPartOfAnyPrefab));
            foreach (Transform child in current)
            {
                CollectComponentsToRemove(list, child);
            }
        }

        private List<RemovedComponent> CollectComponentsToRestore(List<RemovedComponent> list, Transform current)
        {
            if (!PrefabUtility.IsPartOfAnyPrefab(current.gameObject)) return list;
            list.AddRange(PrefabUtility.GetRemovedComponents(current.gameObject));
            foreach (Transform child in current)
            {
                CollectComponentsToRestore(list, child);
            }

            return list;
        }

        public void RemovePrefabComponents()
        {
            var toDelete = new List<Component>();

            foreach (var toRemove in objectsToRemove)
            {
                if (removePrefabComponents) CollectComponentsToRemove(toDelete, toRemove.transform);
            }

            // NOTE: this is non-destructive, all components from prefabs can be restored with no data loss
            foreach (var component in toDelete)
            {
                if (component is Transform) continue;
                DestroyImmediate(component);
            }
        }

        public void RestorePrefabComponents()
        {
            var toRestore = new List<RemovedComponent>();
            foreach (var obj in objectsToRemove)
            {
                CollectComponentsToRestore(toRestore, obj.transform);
            }

            foreach (var removedComponent in toRestore) removedComponent.Revert();
        }

        public void Hide()
        {
            foreach (var obj in objectsToRemove)
            {
                obj.hideFlags |= HideFlags.HideInHierarchy;
            }
        }

        public void Unhide()
        {
            foreach (var obj in objectsToRemove)
            {
                obj.hideFlags &= ~HideFlags.HideInHierarchy;
            }
        }
    }
}