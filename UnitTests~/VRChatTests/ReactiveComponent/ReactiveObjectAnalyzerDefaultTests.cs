#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.ReactiveComponent
{
    public class ReactiveObjectAnalyzerDefaultTests : TestBase
    {
        [Test]
        public void MenuActionInitialState_RespectsDefaultSelectionAndInversion(
            [Values(0f, 1f)] float externalDefault,
            [Values(false, true)] bool isDefault,
            [Values(false, true)] bool automaticValue,
            [Values(0f, 1f)] float controlValue,
            [Values(false, true)] bool inverted
        )
        {
            var root = CreateAvatarWithExternalBoolDefault(externalDefault);
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue, isDefault, automaticValue, inverted);

            var analysis = Analyze(root);
            var targetKey = new ObjectActiveTarget(target);
            var rule = analysis.Shapes[targetKey].actionGroups.Single();
            var initiallyActive = isDefault != inverted;

            Assert.That(rule.IsConstant, Is.False);
            Assert.That(rule.InitiallyActive, Is.EqualTo(initiallyActive));
            Assert.That(analysis.InitialActions.ContainsKey(targetKey), Is.EqualTo(initiallyActive));
            Assert.That(target.activeSelf, Is.True, "Analysis must not apply the initial action itself.");

            if (initiallyActive)
            {
                var action = analysis.InitialActions[targetKey] as DriveActiveState;
                Assert.That(action, Is.Not.Null);
                Assert.That(action!.Active, Is.False);

                using var staticContext = new StaticApplyContext();
                Assert.That(action.ApplyStatic(staticContext), Is.EqualTo(StaticApplyResult.Applied));
                Assert.That(target.activeSelf, Is.False);
            }
        }

        [Test]
        public void BuildContextAnalysis_NullExpressionParameterArray_RetainsDynamicMenuToggle()
        {
            var root = CreateAvatarWithExternalBoolDefault(0);
            root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>().expressionParameters.parameters = null;
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue: 1, isDefault: true, automaticValue: false,
                inverted: false);

            var analysis = Analyze(root);
            var targetKey = new ObjectActiveTarget(target);
            var rule = analysis.Shapes[targetKey].actionGroups.Single();
            var condition = rule.ControllingConditions.Single();

            Assert.That(rule.IsConstant, Is.False);
            Assert.That(condition.Parameter, Is.EqualTo("outfit"));
            Assert.That(condition.IsConstant, Is.False);
            Assert.That(condition.InitiallyActive, Is.True);
            Assert.That(analysis.InitialActions.TryGetValue(targetKey, out var action), Is.True);
            Assert.That(action, Is.TypeOf<DriveActiveState>());
            Assert.That(((DriveActiveState)action!).Active, Is.False);
        }

        private GameObject CreateAvatarWithExternalBoolDefault(float externalDefault)
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);
            root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>().expressionParameters =
                TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>().expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = externalDefault
                }
            };
            return root;
        }

        private void CreateMenuControlledToggle(
            GameObject root,
            GameObject target,
            float controlValue,
            bool isDefault,
            bool automaticValue,
            bool inverted
        )
        {
            var controller = CreateChild(root, "controller");
            var menuItem = controller.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = automaticValue;
            menuItem.isDefault = isDefault;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" },
                value = controlValue
            };

            var toggle = controller.AddComponent<ModularAvatarObjectToggle>();
            toggle.Inverted = inverted;
            toggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = false
            });
        }

        private ReactiveObjectAnalyzer.AnalysisResult Analyze(GameObject root)
        {
            var context = CreateContext(root, WellKnownPlatforms.VRChatAvatar30);
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.ActivateExtensionContextRecursive<ReadablePropertyExtension>();
            try
            {
                return new ReactiveObjectAnalyzer(context).Analyze(root);
            }
            finally
            {
                context.DeactivateAllExtensionContexts();
            }
        }
    }
}

#endif
