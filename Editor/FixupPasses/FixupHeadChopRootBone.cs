using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

#if !MA_VRCSDK3_AVATARS
#region Fallback stub implementation
namespace nadena.dev.modular_avatar.core.editor
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class FixupHeadChopRootBone : Pass<FixupHeadChopRootBone>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            // No-op
        }
    }
}
#endregion
#else

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     When we perform NaNimations, we disable the "update when offscreen" option on the affected renderer.
    ///     This has a side effect: If the root bone is a bone underneath the head, then when the head is scaled down to
    ///     a near-zero size, the bounding box of the renderer is also scaled down to a near-zero size, and is then culled
    ///     in first-person view.
    ///     To avoid this, we detect when the root bone is the head (or a child of the head) and, in this case, create a
    ///     substitute bone with VRCHeadChop to disable this behavior.
    /// </summary>
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class FixupHeadChopRootBone : Pass<FixupHeadChopRootBone>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            Dictionary<Transform, Transform> substitutes = new();

            if (!context.AvatarRootTransform.TryGetComponent<Animator>(out var animator)) return;
            if (animator.avatar == null) return;

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return;

            foreach (var renderer in context.AvatarRootTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var rootBone = renderer.rootBone;
                if (rootBone == null) continue;
                if (rootBone == head || rootBone.IsChildOf(head))
                {
                    if (!substitutes.TryGetValue(rootBone, out var substitute))
                    {
                        substitute = new GameObject("RootBoneSubstitute").transform;
                        substitute.SetParent(context.AvatarRootTransform, false);
                        substitute.localPosition = Vector3.zero;
                        substitute.localRotation = Quaternion.identity;
                        substitute.localScale = Vector3.one;

                        substitute.gameObject.AddComponent<ModularAvatarPBBlocker>();

                        var headChop = substitute.gameObject.AddComponent<VRCHeadChop>();
                        headChop.targetBones = new[]
                        {
                            new VRCHeadChop.HeadChopBone
                            {
                                applyCondition = VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply,
                                scaleFactor = 1,
                                transform = substitute
                            }
                        };

                        substitutes[rootBone] = substitute;
                    }

                    renderer.rootBone = substitute;
                }
            }
        }
    }
}

#endif