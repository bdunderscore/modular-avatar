#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections;
using System.Collections.Immutable;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace UnitTests.ReactiveComponent
{
    public class MenuItemDefaultPreviewTests : TestBase
    {
        private const string ParameterName = "Toggle";

        [TestCase(1, true, false, true)]
        [TestCase(0, true, true, false)]
        [TestCase(1, false, false, true)]
        [TestCase(0, false, true, true)]
        [TestCase(0, false, false, false)]
        public void MAParameterDefaultTakesPrecedenceOverMenuItemDefault(
            float defaultValue, bool explicitDefault, bool menuItemDefault, bool enabled)
        {
            var root = CreateRoot("root");
            AddParameter(root, defaultValue, explicitDefault);
            var (_, target) = CreateMenuToggle(root, root, "toggle", menuItemDefault);

            AssertToggleEnabled(Analyze(root), target, enabled);
        }

        [TestCase(0.995f, false)]
        [TestCase(1.005f, false)]
        [TestCase(0.996f, true)]
        [TestCase(1.004f, true)]
        public void FloatParameterDefaultUsesStrictControlConditionBounds(float defaultValue, bool enabled)
        {
            var root = CreateRoot("root");
            var parameters = AddParameter(root, defaultValue);
            var parameter = parameters.parameters[0];
            parameter.syncType = ParameterSyncType.Float;
            parameters.parameters[0] = parameter;
            var (_, target) = CreateMenuToggle(root, root, "toggle");

            AssertToggleEnabled(Analyze(root), target, enabled);
        }

        [TestCase(0, true, false)]
        [TestCase(1, false, true)]
        public void AvatarDescriptorDefaultIsUsed(float defaultValue, bool menuItemDefault, bool enabled)
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, defaultValue);
            var (_, target) = CreateMenuToggle(root, root, "toggle", menuItemDefault);

            AssertToggleEnabled(Analyze(root), target, enabled);
        }

        [TestCase(0, 1, false, true)]
        [TestCase(1, 0, true, false)]
        public void MAParameterDefaultOverridesAvatarDescriptorDefault(
            float descriptorDefault, float maDefault, bool menuItemDefault, bool enabled)
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, descriptorDefault);
            AddParameter(root, maDefault);
            var (_, target) = CreateMenuToggle(root, root, "toggle", menuItemDefault);

            AssertToggleEnabled(Analyze(root), target, enabled);
        }

        [Test]
        public void ChildMAParameterDefaultOverridesAvatarDescriptorDefault()
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, 0);
            var child = CreateChild(root, "child");
            AddParameter(child, 1);
            var (_, target) = CreateMenuToggle(child, root, "toggle");

            AssertToggleEnabled(Analyze(root), target, true);
        }

        [TestCase("", false)]
        [TestCase("", true)]
        [TestCase("Unregistered", false)]
        [TestCase("Unregistered", true)]
        public void MissingParameterDefaultFallsBackToMenuItemDefault(string parameterName, bool isDefault)
        {
            var root = CreateRoot("root");
            var (_, target) = CreateMenuToggle(root, root, "toggle", isDefault,
                parameterName: parameterName);

            AssertToggleEnabled(Analyze(root), target, isDefault);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void NestedParametersUseOuterExplicitDefaultOrInheritInnerDefault(
            bool outerExplicitDefault, bool enabled)
        {
            var root = CreateRoot("root");
            AddParameter(root, 0, outerExplicitDefault);
            var inner = CreateChild(root, "inner");
            AddParameter(inner, 1);
            var (_, target) = CreateMenuToggle(inner, root, "toggle");

            AssertToggleEnabled(Analyze(root), target, enabled);
        }

        [Test]
        public void RemappedParameterUsesEffectiveParameterDefault()
        {
            var root = CreateRoot("root");
            AddParameter(root, 1, parameterName: "Shared");
            var child = CreateChild(root, "child");
            AddParameter(child, 0, false, "Local", remapTo: "Shared");
            var (_, localTarget) = CreateMenuToggle(child, root, "local", parameterName: "Local");
            var (_, sharedTarget) = CreateMenuToggle(root, root, "shared", parameterName: "Shared");

            var analysis = Analyze(root);

            AssertToggleEnabled(analysis, localTarget, true);
            AssertToggleEnabled(analysis, sharedTarget, true);
        }

        [Test]
        public void AutomaticallyRenamedParametersDoNotShareDefaults()
        {
            var root = CreateRoot("root");
            var first = CreateChild(root, "first");
            var second = CreateChild(root, "second");
            AddParameter(first, 1, internalParameter: true);
            AddParameter(second, 0, internalParameter: true);
            var (_, firstTarget) = CreateMenuToggle(first, root, "firstToggle");
            var (_, secondTarget) = CreateMenuToggle(second, root, "secondToggle", true);

            var analysis = Analyze(root);

            AssertToggleEnabled(analysis, firstTarget, true);
            AssertToggleEnabled(analysis, secondTarget, false);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticValueStillUsesMenuItemDefault(bool menuItemDefault)
        {
            var root = CreateRoot("root");
            AddParameter(root, menuItemDefault ? 0 : 1);
            var (_, target) = CreateMenuToggle(root, root, "toggle", menuItemDefault, true);

            AssertToggleEnabled(Analyze(root), target, menuItemDefault);
        }

        [Test]
        public void ForcedMenuItemOverridesParameterDefault()
        {
            var root = CreateRoot("root");
            AddParameter(root, 0);
            var (menuItem, target) = CreateMenuToggle(root, root, "toggle");

            var analysis = Analyze(root, analyzer => analyzer.ForceMenuItems =
                ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty.Add(ParameterName, menuItem));

            AssertToggleEnabled(analysis, target, true);
        }

        [Test]
        public void ForcedMenuItemCanDisableParameterDefault()
        {
            var root = CreateRoot("root");
            AddParameter(root, 1);
            var (_, target) = CreateMenuToggle(root, root, "toggle", true);

            var analysis = Analyze(root, analyzer => analyzer.ForceMenuItems =
                ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty.Add(ParameterName, null));

            AssertToggleEnabled(analysis, target, false);
        }

        [TestCase(0, 1, true)]
        [TestCase(1, 0, false)]
        public void ForcedPropertyOverridesParameterDefault(float defaultValue, float forcedValue, bool enabled)
        {
            var root = CreateRoot("root");
            AddParameter(root, defaultValue);
            var (_, target) = CreateMenuToggle(root, root, "toggle");

            var analysis = Analyze(root, analyzer => analyzer.ForcePropertyOverrides =
                ImmutableDictionary<string, float>.Empty.Add(ParameterName, forcedValue));

            AssertToggleEnabled(analysis, target, enabled);
        }

        [UnityTest]
        public IEnumerator CachedAnalyzeRefreshesWhenMAParameterDefaultChanges()
        {
            var root = CreateRoot("root");
            var parameters = AddParameter(root, 0);
            var (_, target) = CreateMenuToggle(root, root, "toggle");
            var previousProperties = ROSimulator.PropertyOverrides.Value;
            var previousMenuItems = ROSimulator.MenuItemOverrides.Value;
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            ROSimulator.MenuItemOverrides.Value = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;
            var context = new ComputeContext("Menu Item parameter default preview test");

            try
            {
                AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, false);

                foreach (var defaultValue in new[] { 1f, 0f })
                {
                    var serializedParameters = new SerializedObject(parameters);
                    serializedParameters.FindProperty("parameters").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("defaultValue").floatValue = defaultValue;
                    serializedParameters.ApplyModifiedProperties();
                    yield return null;
                    ComputeContext.FlushInvalidates();

                    Assert.IsTrue(context.IsInvalidated,
                        "Changing an MA Parameters default must invalidate the cached reactive analysis.");

                    context = new ComputeContext("Menu Item parameter default refreshed preview test");
                    AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target,
                        defaultValue == 1);
                }
            }
            finally
            {
                context.Invalidate();
                ROSimulator.PropertyOverrides.Value = previousProperties;
                ROSimulator.MenuItemOverrides.Value = previousMenuItems;
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator CachedAnalyzeRefreshesWhenZeroDefaultIsSpecifiedOrCleared()
        {
            var root = CreateRoot("root");
            var parameters = AddParameter(root, 0, false);
            var (menuItem, target) = CreateMenuToggle(root, root, "toggle", true);
            var previousProperties = ROSimulator.PropertyOverrides.Value;
            var previousMenuItems = ROSimulator.MenuItemOverrides.Value;
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            ROSimulator.MenuItemOverrides.Value = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;
            var context = new ComputeContext("Unspecified parameter default preview test");

            try
            {
                AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, true);

                foreach (var explicitDefault in new[] { true, false })
                {
                    var serializedParameters = new SerializedObject(parameters);
                    serializedParameters.FindProperty("parameters").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("hasExplicitDefaultValue").boolValue = explicitDefault;
                    serializedParameters.ApplyModifiedProperties();
                    yield return null;
                    ComputeContext.FlushInvalidates();

                    Assert.IsTrue(context.IsInvalidated,
                        "Specifying or clearing a zero default must invalidate the cached reactive analysis.");

                    context = new ComputeContext("Zero parameter default refreshed preview test");
                    AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, !explicitDefault);
                    Assert.IsTrue(menuItem.isDefault, "Preview analysis must preserve the Menu Item's default flag.");
                }
            }
            finally
            {
                context.Invalidate();
                ROSimulator.PropertyOverrides.Value = previousProperties;
                ROSimulator.MenuItemOverrides.Value = previousMenuItems;
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator CachedAnalyzeRefreshesWhenExpressionParameterDefaultChanges()
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, 0);
            var parameters = root.GetComponent<VRCAvatarDescriptor>().expressionParameters;
            var (_, target) = CreateMenuToggle(root, root, "toggle");
            var previousProperties = ROSimulator.PropertyOverrides.Value;
            var previousMenuItems = ROSimulator.MenuItemOverrides.Value;
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            ROSimulator.MenuItemOverrides.Value = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;
            var context = new ComputeContext("Expressions parameter default preview test");

            try
            {
                AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, false);

                foreach (var defaultValue in new[] { 1f, 0f })
                {
                    var serializedParameters = new SerializedObject(parameters);
                    serializedParameters.FindProperty("parameters").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("defaultValue").floatValue = defaultValue;
                    serializedParameters.ApplyModifiedProperties();
                    yield return null;
                    ComputeContext.FlushInvalidates();

                    Assert.IsTrue(context.IsInvalidated,
                        "Editing an Expressions Parameters asset must invalidate the cached reactive analysis.");

                    context = new ComputeContext("Expressions parameter default refreshed preview test");
                    AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, defaultValue == 1);
                }
            }
            finally
            {
                context.Invalidate();
                ROSimulator.PropertyOverrides.Value = previousProperties;
                ROSimulator.MenuItemOverrides.Value = previousMenuItems;
                ComputeContext.FlushInvalidates();
            }
        }

        [UnityTest]
        public IEnumerator EditorOnlyParameterSubtreeIsExcludedAndTagChangesInvalidateCachedAnalysis()
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, 0);
            var editorOnly = CreateChild(root, "editorOnly");
            editorOnly.tag = "EditorOnly";
            var parameterObject = CreateChild(editorOnly, "parameters");
            AddParameter(parameterObject, 1);
            var (_, target) = CreateMenuToggle(root, root, "toggle");
            var previousProperties = ROSimulator.PropertyOverrides.Value;
            var previousMenuItems = ROSimulator.MenuItemOverrides.Value;
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            ROSimulator.MenuItemOverrides.Value = ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty;
            var context = new ComputeContext("EditorOnly parameter default preview test");

            try
            {
                AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, false);

                var serializedObject = new SerializedObject(editorOnly);
                serializedObject.FindProperty("m_TagString").stringValue = "Untagged";
                serializedObject.ApplyModifiedProperties();
                yield return null;
                ComputeContext.FlushInvalidates();

                Assert.IsTrue(context.IsInvalidated,
                    "Changing an EditorOnly ancestor's tag must invalidate the cached reactive analysis.");

                context = new ComputeContext("EditorOnly parameter default refreshed preview test");
                AssertToggleEnabled(ReactiveObjectAnalyzer.CachedAnalyze(context, root), target, true);
            }
            finally
            {
                context.Invalidate();
                ROSimulator.PropertyOverrides.Value = previousProperties;
                ROSimulator.MenuItemOverrides.Value = previousMenuItems;
                ComputeContext.FlushInvalidates();
            }
        }

        private ModularAvatarParameters AddParameter(GameObject owner, float defaultValue,
            bool explicitDefault = true, string parameterName = ParameterName, bool internalParameter = false,
            string? remapTo = null)
        {
            var parameters = owner.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = parameterName,
                remapTo = remapTo ?? "",
                defaultValue = defaultValue,
                hasExplicitDefaultValue = explicitDefault,
                syncType = ParameterSyncType.Bool,
                internalParameter = internalParameter,
            });
            return parameters;
        }

        private void SetDescriptorDefault(GameObject root, float defaultValue)
        {
            var parameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            parameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = ParameterName,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = defaultValue,
                }
            };
            root.GetComponent<VRCAvatarDescriptor>().expressionParameters = parameters;
        }

        private (ModularAvatarMenuItem MenuItem, GameObject Target) CreateMenuToggle(GameObject parent,
            GameObject root, string name, bool isDefault = false, bool automaticValue = false,
            string parameterName = ParameterName)
        {
            var menuObject = CreateChild(parent, name);
            var menuItem = menuObject.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName },
                value = 1,
            };
            menuItem.isDefault = isDefault;
            menuItem.automaticValue = automaticValue;

            var target = CreateChild(root, name + "Target");
            var toggle = menuObject.AddComponent<ModularAvatarObjectToggle>();
            toggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = false,
            });
            return (menuItem, target);
        }

        private static ReactiveObjectAnalyzer.AnalysisResult Analyze(GameObject root,
            Action<ReactiveObjectAnalyzer>? configure = null)
        {
            var context = new ComputeContext("Menu Item parameter default preview test");
            try
            {
                var analyzer = new ReactiveObjectAnalyzer(context);
                configure?.Invoke(analyzer);
                return analyzer.Analyze(root);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        private static void AssertToggleEnabled(ReactiveObjectAnalyzer.AnalysisResult analysis,
            GameObject target, bool enabled)
        {
            var hasAction = analysis.InitialActions.TryGetValue(new ObjectActiveTarget(target), out var action);
            Assert.AreEqual(enabled, hasAction, "The preview must select the expected Object Toggle action.");
            if (enabled)
            {
                Assert.IsInstanceOf<DriveActiveState>(action);
                Assert.IsFalse(((DriveActiveState)action!).Active);
            }

            Assert.IsTrue(target.activeSelf, "Preview analysis must preserve the original object's active state.");
        }
    }
}

#endif
