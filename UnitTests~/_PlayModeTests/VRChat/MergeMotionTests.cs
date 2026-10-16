using System.Collections;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnitTests._PlayModeTests;
using UnityEngine;
using UnityEngine.TestTools;

namespace nadena.dev.modular_avatar.test.playmode.vrchat
{
    public class MergeMotionTests : TestBase
    {
        [UnityTest]
        public IEnumerator MotionAppliesWithInactiveReactiveComponent()
        {
            var avatar = CreateRoot();
            var target = CreateChild(avatar, "Target");

            var clip = obj(new AnimationClip());
            clip.SetCurve("Target", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1, 0));
            // Keep the inactive toggle runtime-controlled so its reactive layers are not statically pruned.
            clip.SetCurve("Inactive Toggle", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1, 0));
            var mergeMotion = avatar.AddComponent<ModularAvatarMergeBlendTree>();
            mergeMotion.Motion = clip;
            mergeMotion.PathMode = MergeAnimatorPathMode.Absolute;

            var reactiveObject = CreateChild(avatar, "Inactive Toggle");
            var toggle = reactiveObject.AddComponent<ModularAvatarObjectToggle>();
            toggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = false
            });
            reactiveObject.SetActive(false);

            ProcessAvatar(avatar);
            // Prove that playback, not a static build-time change, applies the merged motion.
            target.SetActive(true);
            ActivateFX(avatar);
            yield return null;
            yield return null;

            Assert.That(target.activeSelf, Is.False,
                "Merge Motion must still animate the target when an inactive reactive component targets it.");
        }
    }
}
