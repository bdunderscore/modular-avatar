using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.plugin;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.GenericTests
{
    public class FloorAdjusterTest : TestBase
    {
        public static object[] Prefabs =
        {
#if MA_VRCSDK3_AVATARS
            ("FloorAdjusterBaseVRChat.prefab", WellKnownPlatforms.VRChatAvatar30),
#endif
            ("FloorAdjusterBaseGenericHuman.prefab", WellKnownPlatforms.Generic)
        };

        [Test]
        public void WhenNonePresent_DoesNotMove(
            [ValueSource(nameof(Prefabs))] (string, string) prefabAndPlatform
        )
        {
            var root = CreatePrefab(prefabAndPlatform.Item1);

            AssertNoMovement(root, prefabAndPlatform.Item2);
        }
        
        [Test]
        public void WhenTwoPresent_DoesNotMove(
            [ValueSource(nameof(Prefabs))] (string, string) prefabAndPlatform
        )
        {
            var root = CreatePrefab(prefabAndPlatform.Item1);

            AddAdjuster(root, Vector3.up * 0.2f);
            AddAdjuster(root, Vector3.up * 0.2f);

            var errors = AssertNoMovement(root, prefabAndPlatform.Item2);
            Assert.IsTrue(errors.Any(e => e.TheError is SimpleError { TitleKey: "error.floor_adjuster.multiple_active" }));
        }
        
        [Test]
        public void WhenOnePresent_DoesMove(
            [ValueSource(nameof(Prefabs))] (string, string) prefabAndPlatform
        )
        {
            var root = CreatePrefab(prefabAndPlatform.Item1);

            AddAdjuster(root, Vector3.up * -0.2f);

            var errors = AssertMovement(root, prefabAndPlatform.Item2, -0.2f);
            Assert.IsEmpty(errors);
        }
        
        [Test]
        public void WhenOneActiveOneInactive_DoesMove(
            [ValueSource(nameof(Prefabs))] (string, string) prefabAndPlatform
        )
        {
            var root = CreatePrefab(prefabAndPlatform.Item1);

            AddAdjuster(root, Vector3.up * -0.2f);
            AddAdjuster(root, Vector3.up * -0.3f).SetActive(false);

            var errors = AssertMovement(root, prefabAndPlatform.Item2, -0.2f);
            Assert.IsTrue(errors.Any(e => e.TheError is SimpleError { TitleKey: "error.floor_adjuster.multiple_inactive" }));
        }
        
        [Test]
        public void WhenDriftingAlongXZ_OnlyYAxisIsUsed(
            [ValueSource(nameof(Prefabs))] (string, string) prefabAndPlatform
        )
        {
            var root = CreatePrefab(prefabAndPlatform.Item1);

            AddAdjuster(root, new Vector3(12, -0.2f, 34));

            var errors = AssertMovement(root, prefabAndPlatform.Item2, -0.2f);
            Assert.IsEmpty(errors);
        }
        
        [Test]
        public void WhenAllInactive_DoesNotMove(
            [ValueSource(nameof(Prefabs))] (string, string) prefabAndPlatform
        )
        {
            var root = CreatePrefab(prefabAndPlatform.Item1);

            AddAdjuster(root, Vector3.up * -0.2f).SetActive(false);

            var errors = AssertNoMovement(root, prefabAndPlatform.Item2);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void WhenUsedOnGenericAvatar_DoesNotCrash()
        {
            var root = CreateCommonPrefab("MinimalAvatar.prefab");
            
            AddAdjuster(root, Vector3.up * -0.2f);
            
            var errors = ErrorReport.CaptureErrors(() => FloorAdjusterPass.TestExecute(CreateContext(root, WellKnownPlatforms.Generic)));
            Assert.IsTrue(errors.All(e => e.TheError.Severity < ErrorSeverity.Error));
        }
        
        GameObject AddAdjuster(GameObject root, Vector3 offset)
        {
            var child = CreateChild(root, "adjuster");
            child.AddComponent<ModularAvatarFloorAdjuster>();
            
            child.transform.position += offset;

            return child;
        }

        List<ErrorContext> AssertMovement(GameObject root, string platform, float offset)
        {
            var context = CreateContext(root, platform);

            var animator = context.AvatarRootObject.GetComponent<Animator>();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipsPos = hips.position - Vector3.up * offset;
            var viewpoint = context.PlatformProvider.ExtractCommonAvatarInfo(context.AvatarRootObject)!.EyePosition!.Value;
            viewpoint -= Vector3.up * offset;
            var avatar = animator.avatar;
            
            var errors = ErrorReport.CaptureErrors(() =>
            {
                FloorAdjusterPass.TestExecute(context);
                new RebindHumanoidAvatar(context).Process();
            });
            
            Assert.AreNotSame(avatar, animator.avatar);

            var hbHips = animator.avatar.humanDescription.skeleton.First(b => b.name == hips.gameObject.name);
            Assert.That(Vector3.Distance(hips.position,hipsPos), Is.LessThan(0.001f));
            var actualViewpoint =
                context.PlatformProvider.ExtractCommonAvatarInfo(context.AvatarRootObject)!.EyePosition!.Value;
            if (platform != WellKnownPlatforms.Generic)
            {
                // TODO - needs NDMF-side support
                Assert.That(Vector3.Distance(actualViewpoint, viewpoint), Is.LessThan(0.001f));
            }
            Assert.That(Vector3.Distance(hips.localPosition, hbHips.position), Is.LessThan(0.001f));

            return errors;
        }
        
        List<ErrorContext> AssertNoMovement(GameObject root, string platform)
        {
            var context = CreateContext(root, platform);

            var animator = context.AvatarRootObject.GetComponent<Animator>();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var hipsPos = hips.position;
            var viewpoint = context.PlatformProvider.ExtractCommonAvatarInfo(context.AvatarRootObject)!.EyePosition!.Value;
            var avatar = animator.avatar;
            
            var errors = ErrorReport.CaptureErrors(() => FloorAdjusterPass.TestExecute(context));
            
            Assert.AreSame(avatar, animator.avatar);
            Assert.AreEqual(viewpoint, context.PlatformProvider.ExtractCommonAvatarInfo(context.AvatarRootObject)!.EyePosition!.Value);
            Assert.AreEqual(hipsPos, hips.position);

            return errors;
        }
    }
}