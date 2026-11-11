#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
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
            var root = CreateAvatarWithoutOutfitDefault();
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

        [TestCase(0f, 0f, false)]
        [TestCase(0f, 1f, true)]
        [TestCase(1f, 0f, true)]
        [TestCase(1f, 1f, false)]
        public void NoBuildContextPreview_UsesExpressionAssetDefault(
            float expressionDefault,
            float controlValue,
            bool isDefault
        )
        {
            var root = CreateAvatarWithExternalBoolDefault(expressionDefault);
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue, isDefault, automaticValue: false,
                inverted: false);

            AssertInitialAction(AnalyzeForPreview(root), target, expressionDefault == controlValue);
        }

        [Test]
        public void ManualMenuActionInitialState_UsesParameterProviderDefault(
            [Values(0f, 1f)] float controlValue,
            [Values(0f, 1f)] float providerDefault,
            [Values(false, true)] bool isDefault
        )
        {
            var root = CreateAvatarWithoutOutfitDefault();
            AddParameterProvider(root, "outfit", providerDefault);
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue, isDefault, automaticValue: false,
                inverted: false);

            AssertInitialAction(AnalyzeForPreview(root), target, providerDefault == controlValue);
        }

        [Test]
        public void ManualMenuActionInitialState_FallsBackToDefaultSelectionWithoutProvider(
            [Values(0f, 1f)] float controlValue,
            [Values(false, true)] bool isDefault
        )
        {
            var root = CreateAvatarWithoutOutfitDefault();
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue, isDefault, automaticValue: false,
                inverted: false);

            AssertInitialAction(AnalyzeForPreview(root), target, isDefault);
        }

        [TestCase(false, 1f)]
        [TestCase(true, 0f)]
        public void AutoMenuActionInitialState_UsesDefaultSelectionDespiteProviderAndStaleValue(
            bool isDefault,
            float staleControlValue
        )
        {
            var root = CreateAvatarWithoutOutfitDefault();
            AddParameterProvider(root, "outfit", isDefault ? 1 - staleControlValue : staleControlValue);
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, staleControlValue, isDefault,
                automaticValue: true, inverted: false);

            AssertInitialAction(AnalyzeForPreview(root), target, isDefault);
        }

        [Test]
        public void ManualMenuActionInitialState_ResolvesLocalRemappedProvider()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            root.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = "outfit",
                    remapTo = "local_outfit",
                    syncType = ParameterSyncType.Bool,
                    defaultValue = 1,
                    hasExplicitDefaultValue = true
                }
            };

            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);

            AssertInitialAction(AnalyzeForPreview(root), target, expected: true);
        }

        [Test]
        public void HierarchyRemappedProviders_HaveIndependentDefaultsAndOverrides()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            var leftScope = CreateChild(root, "left scope");
            var rightScope = CreateChild(root, "right scope");
            AddRemappedParameterProvider(leftScope, "outfit", "left_outfit", 0);
            AddRemappedParameterProvider(rightScope, "outfit", "right_outfit", 1);

            var leftTarget = CreateChild(root, "left target");
            var rightTarget = CreateChild(root, "right target");
            var leftController = CreateMenuControlledToggle(leftScope, leftTarget, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);
            var rightController = CreateMenuControlledToggle(rightScope, rightTarget, controlValue: 1,
                isDefault: false, automaticValue: false, inverted: false);

            var context = new ComputeContext("hierarchy remapped providers");
            try
            {
                var analysis = new ReactiveObjectAnalyzer(context).Analyze(root);
                AssertInitialAction(analysis, leftTarget, expected: false);
                AssertInitialAction(analysis, rightTarget, expected: true);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }

            var menuOverrideContext = new ComputeContext("hierarchy remapped menu override");
            try
            {
                var analyzer = new ReactiveObjectAnalyzer(menuOverrideContext)
                {
                    ForceMenuItems = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty
                        .Add("left_outfit", leftController.GetComponent<ModularAvatarMenuItem>())
                };

                var analysis = analyzer.Analyze(root);
                AssertInitialAction(analysis, leftTarget, expected: true);
                AssertInitialAction(analysis, rightTarget, expected: true);
            }
            finally
            {
                menuOverrideContext.Invalidate();
                ComputeContext.FlushInvalidates();
            }

            var propertyOverrideContext = new ComputeContext("hierarchy remapped property override");
            try
            {
                var analyzer = new ReactiveObjectAnalyzer(propertyOverrideContext)
                {
                    ForcePropertyOverrides =
                        ImmutableDictionary<string, float>.Empty.Add("right_outfit", 0)
                };

                var analysis = analyzer.Analyze(root);
                AssertInitialAction(analysis, leftTarget, expected: false);
                AssertInitialAction(analysis, rightTarget, expected: false);
            }
            finally
            {
                propertyOverrideContext.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [Test]
        public void ForcedMenuSelection_TakesPrecedenceOverParameterProviderDefault()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            AddParameterProvider(root, "outfit", 1);
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);

            var context = new ComputeContext("forced menu selection");
            try
            {
                var analyzer = new ReactiveObjectAnalyzer(context)
                {
                    ForceMenuItems = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty.Add("outfit", null)
                };

                AssertInitialAction(analyzer.Analyze(root), target, expected: false);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator CachedAnalyze_InvalidatesWhenParameterProviderDefaultChanges()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            var provider = AddParameterProvider(root, "outfit", 0);
            var target = CreateChild(root, "target");
            CreateMenuControlledToggle(root, target, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);
            var context = new ComputeContext("parameter provider default invalidation");
            try
            {
                AssertInitialAction(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, expected: false);

                var serializedProvider = new SerializedObject(provider);
                serializedProvider.FindProperty("parameters").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("defaultValue").floatValue = 1;
                serializedProvider.ApplyModifiedProperties();

                for (var i = 0; i < 10 && !context.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }

                Assert.That(context.IsInvalidated, Is.True,
                    "Changing a parameter provider default must invalidate the cached reactive analysis.");

                var refreshedContext = new ComputeContext("refreshed parameter provider default invalidation");
                try
                {
                    AssertInitialAction(ReactiveObjectAnalyzer.CachedAnalyze(refreshedContext, root), target,
                        expected: true);
                }
                finally
                {
                    refreshedContext.Invalidate();
                    ComputeContext.FlushInvalidates();
                }
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator CachedAnalyze_InvalidatesWhenAutomaticValueChanges()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            AddParameterProvider(root, "outfit", 0);
            var target = CreateChild(root, "target");
            var controller = CreateMenuControlledToggle(root, target, controlValue: 1, isDefault: true,
                automaticValue: false, inverted: false);
            var context = new ComputeContext("automatic menu value invalidation");
            try
            {
                AssertInitialAction(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, expected: false);

                var serializedMenuItem = new SerializedObject(controller.GetComponent<ModularAvatarMenuItem>());
                serializedMenuItem.FindProperty("automaticValue").boolValue = true;
                serializedMenuItem.ApplyModifiedProperties();

                for (var i = 0; i < 10 && !context.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }

                Assert.That(context.IsInvalidated, Is.True,
                    "Changing automatic value mode must invalidate the cached reactive analysis.");

                var refreshedContext = new ComputeContext("refreshed automatic menu value invalidation");
                try
                {
                    AssertInitialAction(ReactiveObjectAnalyzer.CachedAnalyze(refreshedContext, root), target,
                        expected: true);
                }
                finally
                {
                    refreshedContext.Invalidate();
                    ComputeContext.FlushInvalidates();
                }
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator CachedAnalyze_InvalidatesWhenMenuMovesBetweenRemappedScopes()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            var leftScope = CreateChild(root, "left scope");
            var rightScope = CreateChild(root, "right scope");
            AddRemappedParameterProvider(leftScope, "outfit", "left_outfit", 0);
            AddRemappedParameterProvider(rightScope, "outfit", "right_outfit", 1);

            var target = CreateChild(root, "target");
            var controller = CreateMenuControlledToggle(leftScope, target, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);
            var context = new ComputeContext("remapped menu move invalidation");
            try
            {
                AssertInitialAction(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, expected: false);

                Undo.SetTransformParent(controller.transform, rightScope.transform,
                    "Move menu between remapped scopes");
                for (var i = 0; i < 10 && !context.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }

                Assert.That(context.IsInvalidated, Is.True,
                    "Moving a menu between remapped scopes must invalidate the cached reactive analysis.");

                var refreshedContext = new ComputeContext("refreshed remapped menu move invalidation");
                try
                {
                    AssertInitialAction(ReactiveObjectAnalyzer.CachedAnalyze(refreshedContext, root), target,
                        expected: true);
                }
                finally
                {
                    refreshedContext.Invalidate();
                    ComputeContext.FlushInvalidates();
                }
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator SimulatorMenuOverride_UsesEffectiveScopedParameterName()
        {
            var root = CreateAvatarWithoutOutfitDefault();
            var leftScope = CreateChild(root, "left scope");
            var rightScope = CreateChild(root, "right scope");
            AddRemappedParameterProvider(leftScope, "outfit", "left_outfit", 0);
            AddRemappedParameterProvider(rightScope, "outfit", "right_outfit", 0);

            var leftTarget = CreateChild(root, "left target");
            var rightTarget = CreateChild(root, "right target");
            var leftController = CreateMenuControlledToggle(leftScope, leftTarget, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);
            CreateMenuControlledToggle(rightScope, rightTarget, controlValue: 1, isDefault: false,
                automaticValue: false, inverted: false);

            var oldSelection = Selection.activeGameObject;
            var oldPropertyOverrides = ROSimulator.PropertyOverrides.Value;
            var oldMenuOverrides = ROSimulator.MenuItemOverrides.Value;
            ROSimulator? window = null;
            try
            {
                Selection.activeGameObject = leftController;
                window = EditorWindow.GetWindow<ROSimulator>();
                yield return null;
                yield return null;

                ROSimulator.OpenDebugger(leftController);

                Button? enableButton = null;
                for (var i = 0; i < 10 && enableButton == null; i++)
                {
                    yield return null;
                    enableButton = window.rootVisualElement
                        .Q<VisualElement>("this-menu-override")
                        ?.Q<Button>(className: "btn-enable");
                }

                Assert.That(enableButton, Is.Not.Null, "The simulator must expose the menu enable override button.");
                enableButton!.Focus();
                using (var submitEvent = NavigationSubmitEvent.GetPooled())
                {
                    enableButton.SendEvent(submitEvent);
                }

                var context = new ComputeContext("simulator scoped menu override");
                try
                {
                    var analysis = ReactiveObjectAnalyzer.CachedAnalyze(context, root);
                    AssertInitialAction(analysis, leftTarget, expected: true);
                    AssertInitialAction(analysis, rightTarget, expected: false);
                }
                finally
                {
                    context.Invalidate();
                    ComputeContext.FlushInvalidates();
                }
            }
            finally
            {
                window?.Close();
                Selection.activeGameObject = oldSelection;
                ROSimulator.PropertyOverrides.Value = oldPropertyOverrides;
                ROSimulator.MenuItemOverrides.Value = oldMenuOverrides;
                ComputeContext.FlushInvalidates();
            }
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

        private GameObject CreateAvatarWithoutOutfitDefault()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);
            root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>().expressionParameters =
                TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>().expressionParameters.parameters =
                System.Array.Empty<VRCExpressionParameters.Parameter>();
            return root;
        }

        private GameObject CreateMenuControlledToggle(
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

            return controller;
        }

        private static ModularAvatarParameters AddParameterProvider(GameObject obj, string parameter, float defaultValue)
        {
            var provider = obj.AddComponent<ModularAvatarParameters>();
            provider.parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = parameter,
                    syncType = ParameterSyncType.Bool,
                    defaultValue = defaultValue,
                    hasExplicitDefaultValue = true
                }
            };
            return provider;
        }

        private static void AddRemappedParameterProvider(
            GameObject obj,
            string parameter,
            string effectiveParameter,
            float defaultValue
        )
        {
            var provider = obj.AddComponent<ModularAvatarParameters>();
            provider.parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = parameter,
                    remapTo = effectiveParameter,
                    syncType = ParameterSyncType.Bool,
                    defaultValue = defaultValue,
                    hasExplicitDefaultValue = true
                }
            };
        }

        private static void AssertInitialAction(
            ReactiveObjectAnalyzer.AnalysisResult analysis,
            GameObject target,
            bool expected
        )
        {
            var targetKey = new ObjectActiveTarget(target);
            Assert.That(analysis.InitialActions.TryGetValue(targetKey, out var action), Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(action, Is.TypeOf<DriveActiveState>());
                Assert.That(((DriveActiveState)action!).Active, Is.False);
            }
        }

        private ReactiveObjectAnalyzer.AnalysisResult AnalyzeForPreview(GameObject root)
        {
            var context = new ComputeContext("parameter provider preview");
            try
            {
                return new ReactiveObjectAnalyzer(context).Analyze(root);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
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
