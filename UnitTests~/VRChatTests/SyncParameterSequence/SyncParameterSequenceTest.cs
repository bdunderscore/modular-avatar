#if MA_VRCSDK3_AVATARS

using System.IO;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.SyncParameterSequence;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.ModularAvatarSyncParameterSequence;

namespace UnitTests.SyncParameterSequence
{
    public class SyncParameterSequenceTest : TestBase
    {
        private string _blueprintId = "";

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            SyncParameterSequencePass.LastPrimaryTarget.Value = null;
            SyncParameterSequencePass.OverrideBuildTarget = null;
        }

        [TearDown]
        public override void Teardown()
        {
            SyncParameterSequencePass.OverrideBuildTarget = null;
            SyncParameterSequencePass.LastPrimaryTarget.Value = null;

            if (!string.IsNullOrEmpty(_blueprintId))
            {
                var path = Path.Combine("Library/nadena.dev.modular-avatar/AvatarParameterInfo", _blueprintId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            base.Teardown();
        }

        [Test]
        public void WhenPrimaryPlatformChangedFromPriorBuild_Warns()
        {
            var root = CreateConfiguredRoot(Platform.Android);
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);

            SyncParameterSequencePass.OverrideBuildTarget = BuildTarget.Android;

            // First build: establish a primary platform record for Android.
            var firstErrors = ErrorReport.CaptureErrors(() => SyncParameterSequencePass.ExecuteStatic(context));
            Assert.IsTrue(firstErrors.All(e => e.TheError.Severity < ErrorSeverity.Error));
            Assert.IsFalse(firstErrors.Any(e => IsWarningKey(e, "validation.syncparamsequence.primary_platform_changed")));

            // Change the component's primary platform to PC without changing the build target.
            var sequence = root.GetComponent<ModularAvatarSyncParameterSequence>();
            sequence.PrimaryPlatform = Platform.PC;

            var errors = ErrorReport.CaptureErrors(() => SyncParameterSequencePass.ExecuteStatic(context));

            Assert.IsTrue(errors.Any(e => IsWarningKey(e, "validation.syncparamsequence.primary_platform_changed")),
                "Expected a warning because the primary platform setting changed from the recorded build.");
        }

        [Test]
        public void WhenNoPrimaryRecordAndBuildingSecondary_Warns()
        {
            var root = CreateConfiguredRoot(Platform.PC);
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);

            // Build for Android while the configured primary platform is PC, with no prior primary record.
            SyncParameterSequencePass.OverrideBuildTarget = BuildTarget.Android;

            var errors = ErrorReport.CaptureErrors(() => SyncParameterSequencePass.ExecuteStatic(context));

            Assert.IsFalse(errors.Any(e => e.TheError.Severity == ErrorSeverity.Error));
            Assert.IsTrue(errors.Any(e => IsWarningKey(e, "validation.syncparamsequence.no_primary_record")),
                "Expected a warning because no primary platform record exists and this is a secondary build.");
        }

        [Test]
        public void WhenBuildingPrimaryFirst_NoWarning()
        {
            var root = CreateConfiguredRoot(Platform.PC);
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);

            SyncParameterSequencePass.OverrideBuildTarget = BuildTarget.StandaloneWindows64;

            var errors = ErrorReport.CaptureErrors(() => SyncParameterSequencePass.ExecuteStatic(context));

            Assert.IsFalse(errors.Any(e => e.TheError.Severity == ErrorSeverity.Error));
            Assert.IsFalse(errors.Any(e =>
                IsWarningKey(e, "validation.syncparamsequence.primary_platform_changed") ||
                IsWarningKey(e, "validation.syncparamsequence.no_primary_record")));
        }

        [Test]
        public void WhenPrimaryBuiltBeforeSecondary_NoRecordMissingWarning()
        {
            var root = CreateConfiguredRoot(Platform.PC);
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);

            // Build the primary platform first to create a record.
            SyncParameterSequencePass.OverrideBuildTarget = BuildTarget.StandaloneWindows64;
            var primaryErrors = ErrorReport.CaptureErrors(() => SyncParameterSequencePass.ExecuteStatic(context));
            Assert.IsFalse(primaryErrors.Any(e => e.TheError.Severity == ErrorSeverity.Error));

            // Build a secondary platform; there should be no "missing primary record" warning.
            SyncParameterSequencePass.OverrideBuildTarget = BuildTarget.Android;
            var secondaryErrors = ErrorReport.CaptureErrors(() => SyncParameterSequencePass.ExecuteStatic(context));

            Assert.IsFalse(secondaryErrors.Any(e => e.TheError.Severity == ErrorSeverity.Error));
            Assert.IsFalse(secondaryErrors.Any(e =>
                IsWarningKey(e, "validation.syncparamsequence.primary_platform_changed") ||
                IsWarningKey(e, "validation.syncparamsequence.no_primary_record")));
        }

        private GameObject CreateConfiguredRoot(Platform primaryPlatform)
        {
            var root = CreateRoot("SyncParameterSequenceTest");

            var pipelineManager = root.GetComponent<PipelineManager>();
            _blueprintId = pipelineManager.blueprintId = "avtr-" + GUID.Generate();

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            descriptor.expressionParameters.parameters = new VRCExpressionParameters.Parameter[0];

            var sequence = root.AddComponent<ModularAvatarSyncParameterSequence>();
            sequence.PrimaryPlatform = primaryPlatform;

            return root;
        }

        private static bool IsWarningKey(ErrorContext errorContext, string titleKey)
        {
            return errorContext.TheError is SimpleError simpleError
                   && simpleError.Severity == ErrorSeverity.NonFatal
                   && simpleError.TitleKey == titleKey;
        }
    }
}

#endif
