#if UNITY_EDITOR

using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDKBase;
#endif

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// This component was previously used to trigger avatar processing when entering play mode. This functionality has
    /// moved to NDMF, so we leave here a stub to clean up detritus left behind from older versions of MA.
    /// We create it on a hidden object via AvatarTagObject's OnValidate, and it will proceed to add MAAvatarActivator
    /// components to all avatar roots which contain MA components. This MAAvatarActivator component then performs MA
    /// processing on Awake.
    ///
    /// Note that we do not directly process the avatars from MAActivator. This is to avoid processing avatars that are
    /// initially inactive in the scene (which can have high overhead if the user has a lot of inactive avatars in the
    /// scene).
    /// </summary>
    [AddComponentMenu("/")]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-9998)]
    public class Activator : MonoBehaviour, IEditorOnly
    {
        private void Update()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [AddComponentMenu("/")]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-9997)]
    public class AvatarActivator : MonoBehaviour, IEditorOnly
    {
        private void Update()
        {
            DestroyImmediate(this);
        }
    }
}
#endif