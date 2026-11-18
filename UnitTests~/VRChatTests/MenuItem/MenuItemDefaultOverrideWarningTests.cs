#if MA_VRCSDK3_AVATARS

#nullable enable

using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.test.Tests;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.MenuItem
{
    public class MenuItemDefaultOverrideWarningTests : TestBase
    {
        [TestCase(ParameterSyncType.Bool, 0, true, true)]
        [TestCase(ParameterSyncType.Float, 0, true, true)]
        [TestCase(ParameterSyncType.Int, 0, true, false)]
        [TestCase(ParameterSyncType.Int, 0, false, false)]
        [TestCase(ParameterSyncType.Bool, 0, false, false)]
        [TestCase(ParameterSyncType.Float, 0, false, false)]
        [TestCase(ParameterSyncType.Bool, 1, true, false)]
        [TestCase(ParameterSyncType.Float, 1, true, false)]
        [TestCase(ParameterSyncType.Bool, 1, false, true)]
        [TestCase(ParameterSyncType.Float, 1, false, true)]
        [TestCase(ParameterSyncType.Int, 1, false, false)]
        public void ConfiguredDefaultsRespectMinimalWarningPredicate(
            ParameterSyncType type, float configuredDefault, bool selected, bool expectedWarning)
        {
            var root = CreateRoot("root");
            AddParameter(root, "outfit", type, configuredDefault, explicitDefault: true);
            var item = AddItem(root, "outfit", automatic: true, selected: selected);

            var result = MenuItemDefaultOverrideWarning.GetOverride(item);
            Assert.AreEqual(expectedWarning, result.HasValue);
            if (expectedWarning)
            {
                Assert.That(result.Value.ParameterName, Is.EqualTo("outfit"));
                Assert.That(result.Value.PreviousDefaultValue, Is.EqualTo(configuredDefault));
                Assert.That(result.Value.NewDefaultValue, Is.EqualTo(selected ? 1 : 0));
            }
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void FractionalFloatDefaultWarnsForBothAutomaticSelections(bool selected, float newDefault)
        {
            var root = CreateRoot("root");
            AddParameter(root, "outfit", ParameterSyncType.Float, 0.5f, explicitDefault: true);
            var item = AddItem(root, "outfit", automatic: true, selected: selected);

            AssertWarning(item, "outfit", 0.5f, newDefault);
        }

        [Test]
        public void ManualTargetDoesNotWarn()
        {
            var root = CreateRoot("root");
            AddParameter(root, "outfit", ParameterSyncType.Bool, 0, explicitDefault: true);
            var item = AddItem(root, "outfit", automatic: false, selected: true, value: 1);

            Assert.IsFalse(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue);
        }

        [TestCase(ParameterSyncType.Bool)]
        [TestCase(ParameterSyncType.Float)]
        public void MultipleMatchingItemsDoNotWarn(ParameterSyncType type)
        {
            var root = CreateRoot("root");
            AddParameter(root, "outfit", type, 0, explicitDefault: true);
            AddItem(root, "outfit", automatic: false, selected: false, value: 0);
            var item = AddItem(root, "outfit", automatic: true, selected: true);

            Assert.IsFalse(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue);
        }

        [TestCase(1, true, false)]
        [TestCase(1, false, true)]
        [TestCase(0, true, true)]
        [TestCase(0, false, false)]
        public void NdmfFirstNonNullDefaultTakesPrecedence(
            float expressionDefault,
            bool selected,
            bool expectedWarning
        )
        {
            var root = CreateRoot("root");
            SetExpressionParameter(root, "outfit", VRCExpressionParameters.ValueType.Bool, expressionDefault);
            AddParameter(root, "outfit", ParameterSyncType.Bool, 1 - expressionDefault, explicitDefault: true);
            var item = AddItem(root, "outfit", automatic: true, selected: selected);

            Assert.That(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue, Is.EqualTo(expectedWarning));
            if (expectedWarning)
            {
                AssertWarning(item, "outfit", expressionDefault, selected ? 1 : 0);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ImplicitParameterWithoutExpressionDeclarationDoesNotWarn(bool selected)
        {
            var root = CreateRoot("root");
            AddParameter(root, "outfit", ParameterSyncType.Bool, 0, explicitDefault: false);
            var item = AddItem(root, "outfit", automatic: true, selected: selected);

            Assert.IsFalse(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue);
        }


        [Test]
        public void GeneratedParameterWithoutDeclarationDoesNotWarn()
        {
            var root = CreateRoot("root");
            var item = AddItem(root, "outfit", automatic: true, selected: true);

            Assert.IsFalse(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue);
        }

        [Test]
        public void ResolvesRemappedParameterBeforeLookingUpDefault()
        {
            var root = CreateRoot("root");
            SetExpressionParameter(root, "outfit", VRCExpressionParameters.ValueType.Bool, 0);
            var parameters = AddParameter(root, "source", ParameterSyncType.Bool, 0, explicitDefault: false,
                remapTo: "outfit");
            var item = AddItem(parameters.gameObject, "source", automatic: true, selected: true);

            AssertWarning(item, "outfit", 0, 1);
        }

        [Test]
        public void InternalParameterDoesNotUseSameNamedOriginalExpressionParameter()
        {
            var root = CreateRoot("root");
            SetExpressionParameter(root, "internal", VRCExpressionParameters.ValueType.Bool, 0);
            var parameters = AddParameter(root, "internal", ParameterSyncType.Bool, 0, explicitDefault: false,
                internalParameter: true);
            var item = AddItem(parameters.gameObject, "internal", automatic: true, selected: true);

            Assert.IsFalse(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue);
        }

        [Test]
        public void FirstMergedConfiguredDefaultWins()
        {
            var root = CreateRoot("root");
            AddParameter(root, "outfit", ParameterSyncType.Bool, 0, explicitDefault: true);
            AddParameter(root, "outfit", ParameterSyncType.Bool, 1, explicitDefault: true);
            var item = AddItem(root, "outfit", automatic: true, selected: true);

            AssertWarning(item, "outfit", 0, 1);
        }

        [Test]
        public void UnsyncedExplicitParentAndSyncedImplicitChildUseConfiguredParentDefault()
        {
            var root = CreateRoot("root");
            var parentParameters = root.AddComponent<ModularAvatarParameters>();
            parentParameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = "outfit",
                syncType = ParameterSyncType.NotSynced,
                defaultValue = 0,
                hasExplicitDefaultValue = true,
            });
            var child = new GameObject("child");
            child.transform.SetParent(root.transform);
            AddParameter(child, "outfit", ParameterSyncType.Bool, 0, explicitDefault: false);
            var item = AddItem(root, "outfit", automatic: true, selected: true);

            AssertWarning(item, "outfit", 0, 1);
        }

        [Test]
        public void ItemWithoutAvatarDoesNotWarn()
        {
            var root = TrackObject(new GameObject("root"));
            AddParameter(root, "outfit", ParameterSyncType.Bool, 0, explicitDefault: true);
            var item = AddItem(root, "outfit", automatic: true, selected: true);

            Assert.IsFalse(MenuItemDefaultOverrideWarning.GetOverride(item).HasValue);
        }

        private static void AssertWarning(
            ModularAvatarMenuItem item,
            string parameterName,
            float previousDefaultValue,
            float newDefaultValue
        )
        {
            var result = MenuItemDefaultOverrideWarning.GetOverride(item);
            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.ParameterName, Is.EqualTo(parameterName));
            Assert.That(result.Value.PreviousDefaultValue, Is.EqualTo(previousDefaultValue));
            Assert.That(result.Value.NewDefaultValue, Is.EqualTo(newDefaultValue));
        }

        private static ModularAvatarParameters AddParameter(GameObject root, string name, ParameterSyncType type,
            float defaultValue, bool explicitDefault, string remapTo = "", bool internalParameter = false)
        {
            var host = new GameObject("parameters");
            host.transform.SetParent(root.transform);
            var parameters = host.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = name,
                remapTo = remapTo,
                syncType = type,
                defaultValue = defaultValue,
                hasExplicitDefaultValue = explicitDefault,
                internalParameter = internalParameter,
            });
            return parameters;
        }

        private static ModularAvatarMenuItem AddItem(GameObject root, string parameter, bool automatic, bool selected,
            float value = 0)
        {
            var host = new GameObject("item");
            host.transform.SetParent(root.transform);
            var item = host.AddComponent<ModularAvatarMenuItem>();
            item.automaticValue = automatic;
            item.isDefault = selected;
            item.PortableControl.Parameter = parameter;
            item.PortableControl.Value = value;

            if (automatic)
            {
                host.AddComponent<ModularAvatarObjectToggle>();
            }

            return item;
        }

        private void SetExpressionParameter(GameObject root, string name, VRCExpressionParameters.ValueType type,
            float defaultValue)
        {
            var parameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            parameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = name,
                    valueType = type,
                    defaultValue = defaultValue,
                }
            };
            root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>().expressionParameters = parameters;
        }
    }
}

#endif
