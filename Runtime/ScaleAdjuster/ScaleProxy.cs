
#region

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("")]
    internal sealed class ScaleProxy : AvatarTagComponent
    {
#if UNITY_EDITOR
        void OnValidate()
        {
            base.OnValidate();
            EditorApplication.delayCall += DeferredValidate;
        }

        void OnDestroy()
        {
            base.OnDestroy();
        }
        
        private void DeferredValidate()
        {
            if (this == null) return;
            
            // Avoid logspam on Unity 2019
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject)) return;

            if (!ProxyManager.ShouldRetain(gameObject))
            {
                SelfDestruct();
            }
        }

        private void SelfDestruct()
        {
            var root = ndmf.runtime.RuntimeUtil.FindAvatarInParents(transform);
            if (root == null)
            {
                root = transform;
                while (root.parent != null) root = root.parent;
            }
            
            ClearOverrides(root);

            if (PrefabUtility.IsPartOfPrefabInstance(gameObject)) return;

            DestroyImmediate(gameObject);
        }

        private void ClearOverrides(Transform root)
        {
            // This clears bone overrides that date back to the 1.9.0-rc.2 implementation, to ease rc.2 -> rc.3
            // migrations. It'll be removed in 1.10.
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.GetComponent<ScaleAdjusterRenderer>()) continue;

                var bones = smr.bones;
                bool changed = false;

                for (var i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == transform)
                    {
                        bones[i] = transform.parent;
                        changed = true;
                    }
                }

                if (changed)
                {
                    smr.bones = bones;
                }
            }
        }
#endif
    }
}