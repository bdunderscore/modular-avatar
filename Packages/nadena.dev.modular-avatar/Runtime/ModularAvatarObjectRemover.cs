using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Object Remover")]
    public class ModularAvatarObjectRemover : AvatarTagComponent
    {
        public bool hideInHierarchy;

        public ModularAvatarObjectRemover()
        {
            EditorApplication.delayCall += Refresh;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (Application.isPlaying) return;
            gameObject.SetActive(false);
            if (hideInHierarchy) Hide();
            // components can be always restored from prefabs, so removing them now does not break anything and will
            // provide better compatibility with other tools
            foreach (var componentsInChild in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                if (PrefabUtility.IsPartOfAnyPrefab(componentsInChild)) DestroyImmediate(componentsInChild);
            }
            foreach (var componentsInChild in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (PrefabUtility.IsPartOfAnyPrefab(componentsInChild)) DestroyImmediate(componentsInChild);
            }
        }

        public void Hide()
        {
            hideInHierarchy = true;
            gameObject.hideFlags |= HideFlags.HideInHierarchy;
        }

        public void Restore()
        {
            hideInHierarchy = false;
            gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
        }
    }
}