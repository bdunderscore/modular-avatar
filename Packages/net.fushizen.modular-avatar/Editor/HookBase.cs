using System;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    public abstract class HookBase : IVRCSDKPreprocessAvatarCallback
    {
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                return OnPreprocessAvatarWrapped(avatarGameObject);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        protected abstract bool OnPreprocessAvatarWrapped(GameObject avatarGameObject);
        public abstract int callbackOrder { get; }
    }
}