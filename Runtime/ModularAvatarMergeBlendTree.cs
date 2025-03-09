#if MA_VRCSDK3_AVATARS

using System;
using API;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Merge Blend Tree")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/merge-blend-tree?lang=auto")]
    public sealed class ModularAvatarMergeBlendTree : AvatarTagComponent, IVirtualizeMotion
    {
        internal static Func<ModularAvatarMergeBlendTree, object, string> GetMotionBasePathCallback
            = (_, _) => "";
        
        // We can't actually reference a BlendTree here because it's not available when building a player build
        public Object BlendTree;
        public MergeAnimatorPathMode PathMode = MergeAnimatorPathMode.Relative;
        public AvatarObjectReference RelativePathRoot = new AvatarObjectReference();

        Motion IVirtualizeMotion.Motion
        {
            get => (Motion)BlendTree;
            set => BlendTree = value;
        }

        string IVirtualizeMotion.GetMotionBasePath(object ndmfBuildContext, bool clearPath = true)
        {
            var path = GetMotionBasePathCallback(this, ndmfBuildContext);

            if (clearPath)
            {
                PathMode = MergeAnimatorPathMode.Absolute;
            }

            return path;
        }
    }
}

#endif