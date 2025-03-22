#if MA_VRCSDK3_AVATARS

using System;
using API;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Merge Motion (Blend Tree)")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/merge-blend-tree?lang=auto")]
    public sealed class ModularAvatarMergeBlendTree : AvatarTagComponent, IVirtualizeMotion
    {
        internal static Func<ModularAvatarMergeBlendTree, object, string> GetMotionBasePathCallback
            = (_, _) => "";

        // Previous versions of this component expected a BlendTree, which is not available in player builds, so this
        // field was made an Object. This can now become a Motion, but unfortunately that would be a breaking change.

        /// <summary>
        ///     The blend tree or other motion to merge.
        /// </summary>
        [Obsolete("Use Motion property instead; this field will be removed in 2.0")] [PublicAPI]
        public Object BlendTree;

        [PublicAPI]
        public MergeAnimatorPathMode PathMode = MergeAnimatorPathMode.Relative;

        [PublicAPI]
        public AvatarObjectReference RelativePathRoot = new AvatarObjectReference();

        [PublicAPI]
        public Motion Motion
        {
            get => ((IVirtualizeMotion)this).Motion;
            set => ((IVirtualizeMotion)this).Motion = value;
        }
        
        Motion IVirtualizeMotion.Motion
        {
#pragma warning disable CS0618 // Type or member is obsolete
            get => (Motion)BlendTree;
            set => BlendTree = value;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        string IVirtualizeMotion.GetMotionBasePath(object ndmfBuildContext, bool clearPath)
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