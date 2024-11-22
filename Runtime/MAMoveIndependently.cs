using System;
using UnityEditor;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDKBase;
#endif

namespace nadena.dev.modular_avatar.core.ArmatureAwase
{
    [ExecuteInEditMode]
    [AddComponentMenu("Modular Avatar/MA Move Independently")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/move-independently?lang=auto")]
    class MAMoveIndependently : MonoBehaviour, IEditorOnly
    {
        private float EPSILON = 0.0000001f;

        [SerializeField]
        private GameObject[] m_groupedBones;

        public GameObject[] GroupedBones
        {
            get => m_groupedBones?.Clone() as GameObject[] ?? Array.Empty<GameObject>();
            set
            {
                m_groupedBones = value.Clone() as GameObject[];
                MaMoveIndependentlyManager.Instance.Activate(this);
            }
        }

        private void OnValidate()
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(this))
            {
                EditorApplication.delayCall += () =>
                {
                    if (this != null) MaMoveIndependentlyManager.Instance.Activate(this);
                };
            }
        }

        private void OnEnable()
        {
            MaMoveIndependentlyManager.Instance.Activate(this);
        }

        private void OnDisable()
        {
            MaMoveIndependentlyManager.Instance.Deactivate(this);
        }
    }
}