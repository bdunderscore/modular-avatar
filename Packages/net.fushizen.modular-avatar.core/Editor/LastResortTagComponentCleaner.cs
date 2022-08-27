using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    /**
     * Ensure that any AvatarTagComponents are purged just before upload.
     */
    public class LastResortTagComponentCleaner : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => 0;
        
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var component in avatarGameObject.GetComponentsInChildren<AvatarTagComponent>(true))
            {
                UnityEngine.Object.DestroyImmediate(component);
            }

            return true;
        }
    }
}