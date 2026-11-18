#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.ParamsUsage
{
    public class MAParametersIntrospectionTests : TestBase
    {
        [TestCase(0, false, false)]
        [TestCase(0, true, true)]
        [TestCase(1, false, true)]
        [TestCase(1, true, true)]
        public void MAParameterDefaultsRespectHasDefaultValue(
            float configuredDefault,
            bool explicitDefault,
            bool expectedDefault
        )
        {
            var root = CreateRoot("root");
            var parameters = root.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = "outfit",
                syncType = ParameterSyncType.Bool,
                defaultValue = configuredDefault,
                hasExplicitDefaultValue = explicitDefault,
            });

            var parameter = GetParameter(root, "outfit");

            Assert.That(parameter.DefaultValue.HasValue, Is.EqualTo(expectedDefault));
            if (expectedDefault)
            {
                Assert.That(parameter.DefaultValue, Is.EqualTo(configuredDefault));
            }
        }

        [TestCase(true, true, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void MenuItemDefaultsAreOnlyProvidedForManualSelectedItems(
            bool automaticValue,
            bool isDefault,
            bool expectedDefault
        )
        {
            var root = CreateRoot("root");
            var item = root.AddComponent<ModularAvatarMenuItem>();
            item.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" },
                value = 2.5f,
            };
            item.automaticValue = automaticValue;
            item.isDefault = isDefault;
            item.gameObject.AddComponent<ModularAvatarObjectToggle>();

            var parameter = GetParameter(root, "outfit");

            Assert.That(parameter.DefaultValue.HasValue, Is.EqualTo(expectedDefault));
            if (expectedDefault)
            {
                Assert.That(parameter.DefaultValue, Is.EqualTo(2.5f));
            }
        }

        private static ProvidedParameter GetParameter(GameObject root, string name)
        {
            return ParameterInfo.ForUI.GetParametersForObject(root)
                .Single(parameter => parameter.EffectiveName == name);
        }
    }
}

#endif
