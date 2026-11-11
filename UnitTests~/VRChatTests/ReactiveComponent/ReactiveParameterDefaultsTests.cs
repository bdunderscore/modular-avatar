#if MA_VRCSDK3_AVATARS

#nullable enable
using System;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.ReactiveComponent
{
    public class ReactiveParameterDefaultsTests : TestBase
    {
        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void WhenAutoParameterPresent_BoolExpressionParameterDefaultIsRetained(bool isDefault, float expectedControlValue)
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 1
                }
            };

            var mesh = CreateChild(root, "mesh");
            mesh.AddComponent<SkinnedMeshRenderer>();

            var menu = CreateChild(root, "menu");
            var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = true;
            menuItem.isDefault = isDefault;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
            };

            var objectToggle = menu.AddComponent<ModularAvatarObjectToggle>();
            objectToggle.Objects.Add(new ToggledObject
            {
                Active = false,
                Object = new AvatarObjectReference(mesh)
            });

            AvatarProcessor.ProcessAvatar(root);

            Assert.AreEqual(expectedControlValue, menuItem.Control.value);
            Assert.AreEqual(isDefault, menuItem.isDefault);
            Assert.AreEqual(1, descriptor.expressionParameters.parameters.Single().defaultValue);
            var fx = FindController(root, VRCAvatarDescriptor.AnimLayerType.FX);
            var controller = (AnimatorController)fx.animatorController;
            Assert.AreEqual(1, controller.parameters.Single(p => p.name == "outfit").defaultFloat);
        }

        // repro of https://github.com/bdunderscore/modular-avatar/issues/2206
        [TestCase(false)]
        [TestCase(true)]
        public void InvertedObjectTogglePreservesEnabledParameterDefault(bool declareAnimatorParameter)
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 1
                }
            };

            var target = CreateChild(root, "target");

            var menu = CreateChild(root, "menu");
            var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = false;
            menuItem.isDefault = true;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" },
                value = 1
            };

            var objectToggle = menu.AddComponent<ModularAvatarObjectToggle>();
            objectToggle.Inverted = true;
            objectToggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = false
            });

            var controller = CreateFxController();
            if (declareAnimatorParameter)
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = "outfit",
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1
                });
            }

            SetFxController(descriptor, controller);

            AvatarProcessor.ProcessAvatar(root);

            var fx = (AnimatorController)descriptor.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            var parameter = fx.parameters.Single(p => p.name == "outfit");
            Assert.IsTrue(target.activeSelf);
            Assert.AreEqual(1, descriptor.expressionParameters.parameters.Single(p => p.name == "outfit").defaultValue);
            Assert.AreEqual(AnimatorControllerParameterType.Float, parameter.type);
            Assert.AreEqual(1f, parameter.defaultFloat);
        }

        [Test]
        public void PreexistingFxDefaultOverridesExpressionParameterDefault()
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 1
                }
            };

            var target = CreateChild(root, "target");
            var menu = CreateChild(root, "menu");
            var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = false;
            menuItem.isDefault = true;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" },
                value = 1
            };

            var objectToggle = menu.AddComponent<ModularAvatarObjectToggle>();
            objectToggle.Inverted = true;
            objectToggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference(target),
                Active = false
            });

            var controller = CreateFxController();
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "outfit",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0
            });
            SetFxController(descriptor, controller);

            AvatarProcessor.ProcessAvatar(root);

            var fx = (AnimatorController)descriptor.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            Assert.AreEqual(0f, fx.parameters.Single(p => p.name == "outfit").defaultFloat);
            Assert.IsFalse(target.activeSelf);
        }

        [Test]
        public void SharedMenuParameterInitialStateIsIndependentOfOrder(
            [Values(false, true)] bool defaultFirst,
            [Values(false, true)] bool declareAnimatorParameter
        )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            if (declareAnimatorParameter)
            {
                var controller = CreateFxController();
                controller.AddParameter("outfit", AnimatorControllerParameterType.Float);
                SetFxController(descriptor, controller);
            }

            var defaultOutfit = CreateChild(root, "defaultOutfit");
            var otherOutfit = CreateChild(root, "otherOutfit");
            otherOutfit.SetActive(false);

            foreach (var isDefault in new[] { defaultFirst, !defaultFirst })
            {
                var child = CreateChild(root, isDefault ? "defaultChoice" : "otherChoice");
                var menuItem = child.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
                };
                menuItem.automaticValue = true;
                menuItem.isDefault = isDefault;

                var toggle = child.AddComponent<ModularAvatarObjectToggle>();
                toggle.Inverted = isDefault;
                toggle.Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(isDefault ? defaultOutfit : otherOutfit),
                    Active = !isDefault
                });
            }

            AvatarProcessor.ProcessAvatar(root);

            Assert.IsTrue(defaultOutfit.activeSelf);
            Assert.IsFalse(otherOutfit.activeSelf);
            var fx = (AnimatorController)descriptor.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            Assert.AreEqual(0f, fx.parameters.Single(p => p.name == "outfit").defaultFloat);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SharedManualMenuParameterInitialStateIsIndependentOfOrderWithoutExternalDefaults(
            bool selectedFirst
        )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            var selectedTarget = CreateChild(root, "selectedTarget");
            selectedTarget.SetActive(false);
            var otherTarget = CreateChild(root, "otherTarget");
            otherTarget.SetActive(false);

            foreach (var selected in new[] { selectedFirst, !selectedFirst })
            {
                var choice = CreateChild(root, selected ? "selectedChoice" : "otherChoice");
                var menuItem = choice.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" },
                    value = selected ? 1 : 2
                };
                menuItem.automaticValue = false;
                menuItem.isDefault = selected;

                var toggle = choice.AddComponent<ModularAvatarObjectToggle>();
                toggle.Objects.Add(new ToggledObject
                {
                    Object = new AvatarObjectReference(selected ? selectedTarget : otherTarget),
                    Active = true
                });
            }

            var context = CreateContext(root, nadena.dev.ndmf.WellKnownPlatforms.VRChatAvatar30);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var fxController = VirtualAnimatorController.Create(animatorServices.ControllerContext.CloneContext);
            animatorServices.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX] = fxController;
            context.ActivateExtensionContextRecursive<InjectROBaseLayer>();
            context.ActivateExtensionContextRecursive<ReadablePropertyExtension>();

            try
            {
                new ReactiveObjectPassV2(context).Execute();

                Assert.AreEqual(1f, fxController.Parameters["outfit"].defaultFloat);
                Assert.IsTrue(selectedTarget.activeSelf);
                Assert.IsFalse(otherTarget.activeSelf);
            }
            finally
            {
                context.DeactivateAllExtensionContexts();
            }
        }

        private AnimatorController CreateFxController()
        {
            var controller = TrackObject(new AnimatorController());
            controller.AddLayer("Idle");
            var state = controller.layers[0].stateMachine.AddState("Idle");
            controller.layers[0].stateMachine.defaultState = state;
            return controller;
        }

        private static void SetFxController(VRCAvatarDescriptor descriptor, AnimatorController controller)
        {
            var layers = descriptor.baseAnimationLayers;
            var index = Array.FindIndex(layers, l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            layers[index].isDefault = false;
            layers[index].animatorController = controller;
            descriptor.baseAnimationLayers = layers;
        }
    }
}

#endif
