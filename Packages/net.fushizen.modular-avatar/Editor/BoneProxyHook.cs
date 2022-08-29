using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    public class BoneProxyHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => HookSequence.SEQ_BONE_PROXY;
        
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var boneProxies = avatarGameObject.GetComponentsInChildren<ModularAvatarBoneProxy>(true);

            foreach (var proxy in boneProxies)
            {
                if (proxy.constraint != null) UnityEngine.Object.DestroyImmediate(proxy.constraint);
                if (proxy.target != null)
                {
                    var oldPath = RuntimeUtil.AvatarRootPath(proxy.gameObject);
                    Transform transform = proxy.transform;
                    transform.SetParent(proxy.target, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    PathMappings.Remap(oldPath, RuntimeUtil.AvatarRootPath(proxy.gameObject));
                }
                Object.DestroyImmediate(proxy);
            }

            return true;
        }
    }
}