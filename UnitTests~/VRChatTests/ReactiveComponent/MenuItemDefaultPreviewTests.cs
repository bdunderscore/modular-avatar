#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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

        [TestCase(0, 1, false)]
        [TestCase(1, 0, false)]
        [TestCase(0, 1, true)]
        [TestCase(1, 0, true)]
        public void PreviewAndBuildAgreeWhenMAOverridesDescriptorDefault(
            float descriptorDefault, float maDefault, bool parameterOnChild)
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, descriptorDefault);
            var parameterOwner = parameterOnChild ? CreateChild(root, "parameters") : root;
            AddParameter(parameterOwner, maDefault);
            var (_, target) = CreateMenuToggle(root, root, "toggle", maDefault == 0);

            AssertPreviewAndBuiltDefault(root, target, maDefault);
        }

        [TestCase(false, 0, 1)]
        [TestCase(false, 1, 1)]
        [TestCase(true, 0, 0)]
        [TestCase(true, 1, 0)]
        public void PreviewAndBuildAgreeForParentDefaultAndChildInheritance(
            bool outerExplicitDefault, float descriptorDefault, float expectedDefault)
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, descriptorDefault);
            AddParameter(root, 0, outerExplicitDefault);
            var child = CreateChild(root, "child");
            AddParameter(child, 1);
            var (_, target) = CreateMenuToggle(child, root, "toggle", expectedDefault == 0);

            AssertPreviewAndBuiltDefault(root, target, expectedDefault);
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public void PreviewAndBuildPreserveSiblingDefaultMergeOrder(
            bool explicitDefaultFirst, float expectedDefault)
        {
            var root = CreateRoot("root");
            SetDescriptorDefault(root, 0);
            var unspecified = CreateChild(root, "unspecified");
            AddParameter(unspecified, 0, false);
            var explicitDefault = CreateChild(root, "explicit");
            AddParameter(explicitDefault, 1);
            if (explicitDefaultFirst) explicitDefault.transform.SetAsFirstSibling();
            var (_, target) = CreateMenuToggle(root, root, "toggle", expectedDefault == 0);

            // An unspecified first sibling does not inherit a later sibling's default during the build.
            AssertPreviewAndBuiltDefault(root, target, expectedDefault);
        }

        [TestCase("", false, 1f)]
        [TestCase("", true, 1f)]
        [TestCase("Unregistered", false, 1f)]
        [TestCase("Unregistered", true, 1f)]
        [TestCase("Unregistered", true, 0.5f)]
        [TestCase("Unregistered", false, 0.999f)]
        public void MissingParameterDefaultFallsBackToMenuItemDefault(string parameterName, bool isDefault,
            float controlValue)
        {
            var root = CreateRoot("root");
            var (menuItem, target) = CreateMenuToggle(root, root, "toggle", isDefault,
                parameterName: parameterName);
            menuItem.PortableControl.Value = controlValue;

            AssertToggleEnabled(Analyze(root), target, isDefault);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnspecifiedMADefaultKeepsMenuFallbackWithoutDescriptorParameter(bool menuDefault)
        {
            var root = CreateRoot("root");
            var parameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            parameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
            root.GetComponent<VRCAvatarDescriptor>().expressionParameters = parameters;
            AddParameter(root, 0, false);
            var (_, target) = CreateMenuToggle(root, root, "toggle", menuDefault);

            // An unspecified authoring default intentionally uses the menu selection in previews.
            // The generated expression parameter's implicit zero does not make this an explicit default.
            AssertToggleEnabled(Analyze(root), target, menuDefault);
            Assert.AreEqual(0, BuildParameterDefault(root));
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

        [Test]
        public void ForcedMenuSelectionIgnoresDuplicateAuthoringValues()
        {
            var root = CreateRoot("root");
            var (first, firstTarget) = CreateMenuToggle(root, root, "first", true, true);
            var (second, secondTarget) = CreateMenuToggle(root, root, "second", false, true);
            var analysis = Analyze(root, analyzer => analyzer.ForceMenuItems =
                ImmutableDictionary<string, ModularAvatarMenuItem?>.Empty.Add(ParameterName, second));
            AssertToggleEnabled(analysis, firstTarget, false);
            AssertToggleEnabled(analysis, secondTarget, true);
            Assert.AreEqual(1, first.PortableControl.Value);
            Assert.AreEqual(1, second.PortableControl.Value);
        }

        [Test]
        public void AllocationDoesNotMutateAuthoringItems()
        {
            var root = CreateRoot("root");
            var (first, _) = CreateMenuToggle(root, root, "first", true, true);
            var (second, _) = CreateMenuToggle(root, root, "second", false, true);
            var assignment = ParameterAssignerPass.Allocate(ParameterName,
                new System.Collections.Generic.List<ParameterAssignerPass.MenuAssignment.Entry>
                {
                    new(first), new(second)
                }, null);
            Assert.AreEqual(0, assignment.Entries[0].Value);
            Assert.AreEqual(1, assignment.Entries[1].Value);
            Assert.AreEqual(0, assignment.Parameter!.defaultValue);
            Assert.AreEqual(1, first.PortableControl.Value);
            Assert.IsTrue(first.isDefault);
            Assert.AreEqual(1, second.PortableControl.Value);
            Assert.IsFalse(second.isDefault);
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

        private void AssertPreviewAndBuiltDefault(GameObject root, GameObject target, float expectedDefault)
        {
            AssertToggleEnabled(Analyze(root), target, expectedDefault == 1);

            Assert.AreEqual(expectedDefault, BuildParameterDefault(root),
                "Preview selection must agree with the default emitted by the production parameter passes.");
        }

        private float BuildParameterDefault(GameObject root)
        {
            var context = CreateContext(root);
            var maContext = context.ActivateExtensionContext<ModularAvatarContext>().BuildContext;
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var errors = ErrorReport.CaptureErrors(() =>
            {
                using (new ObjectRegistryScope(new ObjectRegistry(root.transform)))
                {
                    new RenameParametersHook().OnPreprocessAvatar(root, maContext);
                    new ParameterAssignerPass().TestExecute(context);
                }
            });

            Assert.IsEmpty(errors, "The production parameter passes must build without parameter conflicts.");
            var builtParameter = root.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters
                .Single(p => p.name == ParameterName);
            return builtParameter.defaultValue;
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
