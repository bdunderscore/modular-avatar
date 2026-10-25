#if MA_VRCSDK3_AVATARS

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.ReactiveComponent.ParameterAssignment
{
    public class AutoValueAssignmentTests : TestBase
    {
        [Test]
        public void AutoValueWithMAParameters()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            
            var parameters = child.AddComponent<ModularAvatarParameters>();
            parameters.parameters = new()
            {
                new ParameterConfig()
                {
                    defaultValue = 1.0f,
                    hasExplicitDefaultValue = true,
                    nameOrPrefix = "foo",
                    syncType = ParameterSyncType.Bool
                }
            };
    
            var mami = child.AddComponent<ModularAvatarMenuItem>();
            mami.Control = new()
            {
                parameter = new()
                {
                    name = "foo"
                },
                name = "x"
            };
            mami.automaticValue = true;
    
            child.AddComponent<ModularAvatarMenuInstaller>();
    
            AvatarProcessor.ProcessAvatar(root);
            var menu = root.GetComponent<VRCAvatarDescriptor>().expressionsMenu
                .controls.First(c => c.name == "child");
            Assert.AreEqual(1, menu.value);
        }
    
        public enum ChildType
        {
            Direct,
            Child,
            None
        }
        
        [Test]
        public void ParametersAssignedWhenTogglesControlRC(
            [Values(
                VRCExpressionsMenu.Control.ControlType.Button,
                VRCExpressionsMenu.Control.ControlType.Toggle,
                VRCExpressionsMenu.Control.ControlType.SubMenu,
                VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
                VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
            )]
            VRCExpressionsMenu.Control.ControlType controlType,
            [Values(ChildType.Child, ChildType.Direct, ChildType.None)] ChildType childType
        )
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
    
            var target = CreateChild(root, "target");
            target.AddComponent<SkinnedMeshRenderer>();
            
            var mami = child.AddComponent<ModularAvatarMenuItem>();
            mami.Control = new()
            {
                name = "x",
                type = controlType
            };
            mami.automaticValue = true;
    
            child.AddComponent<ModularAvatarMenuInstaller>();
    
            ModularAvatarObjectToggle? toggle = null;
            switch (childType)
            {
                case ChildType.Direct:
                    toggle = mami.gameObject.AddComponent<ModularAvatarObjectToggle>();
                    break;
                case ChildType.Child:
                    toggle = CreateChild(mami.gameObject, "child").AddComponent<ModularAvatarObjectToggle>();
                    break;
            }
    
            if (toggle != null)
            {
                var obj = new ToggledObject();
                obj.Active = false;
                obj.Object = new();
                obj.Object.Set(target);
                toggle.Objects.Add(obj);
            }
    
            AvatarProcessor.ProcessAvatar(root);
    
            bool shouldHaveParam = (childType != ChildType.None) &&
                                   (controlType == VRCExpressionsMenu.Control.ControlType.Button ||
                                    controlType == VRCExpressionsMenu.Control.ControlType.Toggle);
            
            var vrcExpParams = root.GetComponent<VRCAvatarDescriptor>().expressionParameters;
            var hasParam =
                vrcExpParams.parameters.Any(p => p.name.StartsWith(ParameterAssignerPass.AUTOMATIC_PARAMETER_PREFIX));
            Assert.AreEqual(shouldHaveParam, hasParam);
        }
    
        [Test]
        public void DefaultValueOnParametersControlledParameter(
            [Values(true, false)] bool isDefault
        )
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            var target = CreateChild(child, "menu");
    
            var p = child.AddComponent<ModularAvatarParameters>();
            p.parameters.Add(new()
            {
                defaultValue = 0,
                internalParameter = true,
                nameOrPrefix = "test",
                syncType = ParameterSyncType.Float
            });
    
            target.AddComponent<ModularAvatarMenuInstaller>();
            var mi = target.AddComponent<ModularAvatarMenuItem>();
            mi.label = "test";
            mi.PortableControl.Parameter = "test";
            // Previously, we had an issue when isDefault was true, where the MA Parameters default value
            // would be used even if the value was zero, which is inappropriate for a singleton toggle.
            //
            // Note that we're okay with a zero-value toggle being created if there are _multiple_ toggles,
            // as that might be intended behavior for the user and is consistent with how multiple toggles
            // with an auto param might work.
            mi.isDefault = isDefault;
            mi.automaticValue = true;
            
            AvatarProcessor.ProcessAvatar(root);
            
            var vrcMenu = root.GetComponent<VRCAvatarDescriptor>().expressionsMenu;
            var resultItem = vrcMenu.controls.First(c => c.name == "test");
            Assert.AreEqual(1, resultItem.value);
        }
        
        [Test]
        public void ManuallyAssignedParametersAreNotReplaced()
        {
            TestAssignments(new[] { (false, 1.0f) }, new[] { 1.0f });
            TestAssignments(new[] { (false, 1.0f) }, new[] { 1.0f }, 0);
    
            TestAssignments(new[] { (false, 4.0f) }, new[] { 4.0f });
            TestAssignments(new[] { (false, 4.0f) }, new[] { 4.0f }, 0);
    
            TestAssignments(new[] { (false, 1.0f), (false, 4.0f) }, new[] { 1.0f, 4.0f });
            TestAssignments(new[] { (false, 1.0f), (false, 4.0f) }, new[] { 1.0f, 4.0f }, 0);
        }
    
        [Test]
        public void SingleEntryToggles()
        {
            TestAssignments(new[] { (true, 0.0f) }, new[] { 1.0f });
            TestAssignments(new[] { (true, 0.0f) }, new[] { 1.0f }, 0);
        }
    
        [Test]
        public void MultiEntryToggles()
        {
            TestAssignments(new[] { (true, 0.0f), (true, 0.0f) }, new[] { 0.0f, 1.0f }, 0);
            TestAssignments(new[] { (true, 0.0f), (true, 0.0f) }, new[] { 1.0f, 0.0f }, 1);
            TestAssignments(new[] { (true, 0.0f), (true, 0.0f) }, new[] { 1.0f, 2.0f }, null);
            
            TestAssignments(new[] { (true, 0.0f), (true, 0.0f), (true, 0.0f) }, new[] { 1.0f, 0.0f, 2.0f }, 1);
        }
    
        [Test]
        public void MixedAutoTests()
        {
            TestAssignments(new[] { (false, 2.0f), (true, 0.0f), (true, 0.0f) }, new[] { 2.0f, 1.0f, 3.0f }, null);
            TestAssignments(new[] { (false, 2.7f), (true, 0.0f), (true, 0.0f) }, new[] { 2.7f, 1.0f, 3.0f }, null);
            TestAssignments(new[] { (true, 1.0f), (false, 0.0f) }, new[] { 2.0f, 0.0f }, null, overrideExpectedDefaultValue: 1.0f);
            TestAssignments(new[] { (true, 1.0f), (false, 0.0f) }, new[] { 1.0f, 0.0f }, 0);
            TestAssignments(new[] { (true, 1.0f), (false, 0.0f) }, new[] { 1.0f, 0.0f }, 1);
            
        }
        
        [Test]
        public void IntDefaultMatchesObjectToggleValue()
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "x",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = 1
                }
            };
    
            var obj = CreateChild(root, "obj");
            obj.AddComponent<MeshRenderer>();
    
            var toggle = CreateChild(root, "toggle");
            var menuItem = toggle.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = false;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "x" },
                value = 1
            };
    
            var objectToggle = toggle.AddComponent<ModularAvatarObjectToggle>();
            objectToggle.Objects = new()
            {
                new()
                {
                    Active = false,
                    Object = new AvatarObjectReference(obj)
                }
            };
    
            AvatarProcessor.ProcessAvatar(root);
    
            Assert.IsFalse(obj.activeSelf);
        }
    
        
        [Test]
        public void SharedMenuParameterInitialStateIsIndependentOfOrder(
            [Values(false, true)] bool defaultFirst,
            [Values(false, true)] bool declareAnimatorParameter
        )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            descriptor.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            if (declareAnimatorParameter)
            {
                var controller = new AnimatorController();
                controller.AddParameter("outfit", AnimatorControllerParameterType.Float);
                controller.AddLayer("Idle");
                var state = controller.layers[0].stateMachine.AddState("Idle");
                controller.layers[0].stateMachine.defaultState = state;
                var layers = descriptor.baseAnimationLayers;
                var index = Array.FindIndex(layers, l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
                layers[index].isDefault = false;
                layers[index].animatorController = controller;
                descriptor.baseAnimationLayers = layers;
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

        void TestAssignments(
            (bool, float)[] assignments,
            float[] expectedAssignments,
            int? defaultIndex = null,
            Action<List<ModularAvatarMenuItem>> customize = null,
            float? overrideExpectedDefaultValue = null
        )
        {
            var root = CreateRoot("root");
    
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            avDesc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            avDesc.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
    
            List<ModularAvatarMenuItem> menuItems = new();
            
            foreach (var (auto, value) in assignments)
            {
                var obj = CreateChild(root, "m" + (menuItems.Count));
                var mami = obj.AddComponent<ModularAvatarMenuItem>();
    
                mami.Control = new VRCExpressionsMenu.Control()
                {
                    name = obj.name,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    value = value,
                    parameter = new()
                    {
                        name = "test_parameter"
                    }
                };
    
                mami.automaticValue = auto;
                mami.isDefault = false;
                
                menuItems.Add(mami);
            }
    
            if (defaultIndex.HasValue)
            {
                menuItems[defaultIndex.Value].isDefault = true;
            }
            
            customize?.Invoke(menuItems);
    
            var context = new nadena.dev.ndmf.BuildContext(root, null);
            
            new ParameterAssignerPass().TestExecute(context);
            
            foreach (var (mami, expected) in menuItems.Zip(expectedAssignments, (m, e) => (m, e)))
            {
                Assert.AreEqual(expected, mami.Control.value);
            }
    
            var expectedDefaultValue = overrideExpectedDefaultValue ?? (defaultIndex.HasValue ? expectedAssignments[defaultIndex.Value] : 0);
            Assert.AreEqual(expectedDefaultValue, avDesc.expressionParameters.parameters.Single().defaultValue);
        }
    }
}

#endif
