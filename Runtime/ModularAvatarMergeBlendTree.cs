#if MA_VRCSDK3_AVATARS

using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Merge Blend Tree")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/merge-blend-tree?lang=auto")]
    public sealed class ModularAvatarMergeBlendTree : AvatarTagComponent
    {
        // We can't actually reference a BlendTree here because it's not available when building a player build
        public UnityEngine.Object BlendTree;
        public MergeAnimatorPathMode PathMode = MergeAnimatorPathMode.Relative;
        public AvatarObjectReference RelativePathRoot = new AvatarObjectReference();
    }
}

#endif