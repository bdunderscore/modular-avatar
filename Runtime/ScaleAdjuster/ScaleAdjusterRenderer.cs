#region

using UnityEngine;
using VRC.SDKBase;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// Legacy component from early 1.9.x builds.
    /// </summary>
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    internal class ScaleAdjusterRenderer : MonoBehaviour, IEditorOnly
    {
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;
            
            EditorApplication.delayCall += () =>
            {
                if (this != null) DestroyImmediate(gameObject);
            };
        }
#endif
    }
}