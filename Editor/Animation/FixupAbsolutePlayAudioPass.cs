#if MA_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.animation
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    public class FixupAbsolutePlayAudioPass : Pass<FixupAbsolutePlayAudioPass>
    {
        protected override void Execute(BuildContext context)
        {
            // Older versions of modular avatar did not adjust Animator Play Audio paths when they were absolute paths.
            // Replicate this behavior here.

            // Note that this runs before any object movement.

            var asc = context.Extension<AnimatorServicesContext>();

            foreach (var mama in context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (!mama._wasRelative) continue;

                var pathPrefix = asc.ObjectPathRemapper.GetVirtualPathForObject(mama.gameObject) + "/";

                foreach (var state in asc.ControllerContext.Controllers[mama].AllReachableNodes()
                             .OfType<VirtualState>())
                {
                    foreach (var behavior in state.Behaviours.OfType<VRCAnimatorPlayAudio>())
                    {
                        if (asc.ObjectPathRemapper.GetObjectForPath(behavior.SourcePath) != null) continue;
                        if (behavior.SourcePath.StartsWith(pathPrefix))
                        {
                            behavior.SourcePath = behavior.SourcePath.Substring(pathPrefix.Length);
                        }
                    }
                }
            }
        }
    }
}

#endif