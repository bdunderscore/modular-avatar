using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    public class VRCFuryCompatibilityTest : TestBase
    {
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            ComponentValidation.ForceEnableLegacyVRCFuryError = false;
        }

        public override void Teardown()
        {
            ComponentValidation.ForceEnableLegacyVRCFuryError = false;
            base.Teardown();
        }

        [Test]
        public void LegacyVRCFuryWithReactiveActionReportsFatalError()
        {
            ComponentValidation.ForceEnableLegacyVRCFuryError = true;

            var avatarRoot = CreateRoot("root");
            AddReactiveAction(avatarRoot);

            var errors = ErrorReport.CaptureErrors(() => ComponentValidation.ValidateAll(avatarRoot));

            Assert.IsTrue(errors.Any(error =>
                error.TheError is SimpleError
                {
                    TitleKey: "validation.legacy_vrcfury_error",
                    Severity: ErrorSeverity.Error
                }));
        }

        [Test]
        public void LegacyVRCFuryWithoutDetectedActionsDoesNotReportError()
        {
            ComponentValidation.ForceEnableLegacyVRCFuryError = true;

            var avatarRoot = CreateRoot("root");
            avatarRoot.AddComponent<ModularAvatarObjectToggle>();

            var errors = ErrorReport.CaptureErrors(() => ComponentValidation.ValidateAll(avatarRoot));

            Assert.IsFalse(errors.Any(error =>
                (error.TheError as SimpleError)?.TitleKey == "validation.legacy_vrcfury_error"));
        }

        [Test]
        public void CurrentVRCFuryWithReactiveActionDoesNotReportError()
        {
            var avatarRoot = CreateRoot("root");
            AddReactiveAction(avatarRoot);

            var errors = ErrorReport.CaptureErrors(() => ComponentValidation.ValidateAll(avatarRoot));

            Assert.IsFalse(errors.Any(error =>
                (error.TheError as SimpleError)?.TitleKey == "validation.legacy_vrcfury_error"));
        }

        private static void AddReactiveAction(GameObject avatarRoot)
        {
            var target = new GameObject("target");
            target.transform.SetParent(avatarRoot.transform);

            var controller = new GameObject("controller");
            controller.transform.SetParent(avatarRoot.transform);

            var targetReference = new AvatarObjectReference();
            targetReference.Set(target);

            var objectToggle = controller.AddComponent<ModularAvatarObjectToggle>();
            objectToggle.Objects = new()
            {
                new ToggledObject
                {
                    Object = targetReference,
                    Active = false
                }
            };
        }
    }
}