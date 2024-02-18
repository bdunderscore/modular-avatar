
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
            base.OnDestroy();
            EditorApplication.delayCall += DeferredValidate;
        }

        void OnDestroy()
        {
            ScaleAdjusterRenderer.InvalidateAll();
            base.OnDestroy();
        }
        
        private void DeferredValidate()
        {
            if (this == null) return;
            
            if (GetComponent<ModularAvatarPBBlocker>() == null)
            {
                gameObject.AddComponent<ModularAvatarPBBlocker>();
            }

            var avatar = ndmf.runtime.RuntimeUtil.FindAvatarInParents(transform);
            ClearOverrides(avatar);

            gameObject.hideFlags = HideFlags.HideInHierarchy;

#if MODULAR_AVATAR_DEBUG_HIDDEN
            gameObject.hideFlags = HideFlags.None;
#endif
            hideFlags = HideFlags.None;
            
            var parentObject = transform.parent;
            var parentScaleAdjuster =
                parentObject != null ? parentObject.GetComponent<ModularAvatarScaleAdjuster>() : null;

            if (parentScaleAdjuster == null || parentScaleAdjuster.scaleProxy != transform)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(this))
                {
                    var path = AssetDatabase.GetAssetPath(this);
                    var root = PrefabUtility.LoadPrefabContents(path);

                    foreach (var obj in root.GetComponentsInChildren<ScaleProxy>())
                    {
                        obj.DeferredValidate();
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    SelfDestruct();
                }
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