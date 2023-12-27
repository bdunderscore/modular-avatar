#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("")]
    internal sealed class ScaleProxy : AvatarTagComponent
    {
#if UNITY_EDITOR
        void OnValidate()
        {
            EditorApplication.delayCall += DeferredValidate;
        }

        private void DeferredValidate()
        {
            if (this == null) return;

            gameObject.hideFlags = HideFlags.HideInHierarchy;

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

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
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

            DestroyImmediate(gameObject);
        }
#endif
    }
}