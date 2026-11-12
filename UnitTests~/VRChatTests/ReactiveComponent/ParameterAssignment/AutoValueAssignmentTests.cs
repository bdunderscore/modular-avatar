#if MA_VRCSDK3_AVATARS

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.test.ReactiveComponent.ParameterAssignment
{
    public class AutoValueAssignmentTests : TestBase
    {
        [Test]
        public void AutoValueWithMAParameters(
            [Values(true, false)] bool isDefault
        )
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
            mami.isDefault = isDefault;
    
            child.AddComponent<ModularAvatarMenuInstaller>();
    
            AvatarProcessor.ProcessAvatar(root);
            var menu = root.GetComponent<VRCAvatarDescriptor>().expressionsMenu
                .controls.First(c => c.name == "child");
            Assert.AreEqual(isDefault ? 1 : 0, menu.value);
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

        [TestCase(VRCExpressionParameters.ValueType.Int, true, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Int, false, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, true, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Float, false, 0)]
        public void AutoMenuItemValueRespectsKnownExpressionParameterDefault(
            VRCExpressionParameters.ValueType valueType,
            bool isDefault,
            float expectedControlValue
        )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit",
                    valueType = valueType,
                    defaultValue = 2
                }
            };

            var menu = CreateChild(root, "menu");
            var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = true;
            menuItem.isDefault = isDefault;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
            };

            var context = new nadena.dev.ndmf.BuildContext(root, null);
            new ParameterAssignerPass().TestExecute(context);

            Assert.AreEqual(expectedControlValue, menuItem.Control.value);
            Assert.AreEqual(isDefault, menuItem.isDefault);
            Assert.AreEqual(2, descriptor.expressionParameters.parameters.Single().defaultValue);
        }

        
        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void AutoMenuItemValueRespectsKnownExpressionParameterDefault_BoolParam(bool isDefault, float expectedControlValue)
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

            var menu = CreateChild(root, "menu");
            var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = true;
            menuItem.isDefault = isDefault;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
            };

            var context = new nadena.dev.ndmf.BuildContext(root, null);
            new ParameterAssignerPass().TestExecute(context);

            Assert.AreEqual(expectedControlValue, menuItem.Control.value);
            Assert.AreEqual(isDefault, menuItem.isDefault);
            Assert.AreEqual(1, descriptor.expressionParameters.parameters.Single().defaultValue);
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, 0, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 0, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 1, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 1, false, 0, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0.5f, false, 1, 0.5f)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, false, 1, 0)]
        public void AutomaticValuesRespectDeclaredExternalParameterDefaults(
            VRCExpressionParameters.ValueType valueType,
            float defaultValue,
            bool isDefault,
            float expectedControlValue,
            float expectedParameterDefault
        )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "outfit",
                    valueType = valueType,
                    defaultValue = defaultValue
                }
            };

            var menu = CreateChild(root, "menu");
            var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
            menuItem.automaticValue = true;
            menuItem.isDefault = isDefault;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
            };

            var context = new nadena.dev.ndmf.BuildContext(root, null);
            new ParameterAssignerPass().TestExecute(context);

            Assert.AreEqual(expectedControlValue, menuItem.Control.value);
            var declaredParameter = descriptor.expressionParameters.parameters.Single();
            Assert.AreEqual(valueType, declaredParameter.valueType);
            Assert.AreEqual(expectedParameterDefault, declaredParameter.defaultValue);
        }

        [Test]
        public void AutomaticValueUpdatesPersistentExpressionParameterDefaultWithoutMutatingSource()
        {
            const string assetFolder = "Assets/ZZZ_Temp";
            const string assetPath = assetFolder + "/ExpressionParameters.asset";

            AssetDatabase.CreateFolder("Assets", "ZZZ_Temp");
            try
            {
                var source = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
                source.parameters = new[]
                {
                    new VRCExpressionParameters.Parameter
                    {
                        name = "outfit",
                        valueType = VRCExpressionParameters.ValueType.Bool,
                        defaultValue = 0,
                        saved = true,
                        networkSynced = true
                    }
                };
                AssetDatabase.CreateAsset(source, assetPath);

                var root = CreateRoot("root");
                var descriptor = root.GetComponent<VRCAvatarDescriptor>();
                descriptor.expressionParameters = source;

                var menu = CreateChild(root, "menu");
                var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
                menuItem.automaticValue = true;
                menuItem.isDefault = true;
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
                };

                var context = new nadena.dev.ndmf.BuildContext(root, null);
                new ParameterAssignerPass().TestExecute(context);
                TrackObject(descriptor.expressionParameters);

                Assert.AreEqual(0, source.parameters.Single().defaultValue);
                Assert.AreEqual(1, menuItem.Control.value);

                var processedParameter = descriptor.expressionParameters.parameters.Single();
                Assert.AreEqual("outfit", processedParameter.name);
                Assert.AreEqual(VRCExpressionParameters.ValueType.Bool, processedParameter.valueType);
                Assert.AreEqual(1, processedParameter.defaultValue);
                Assert.IsTrue(processedParameter.saved);
                Assert.IsTrue(processedParameter.networkSynced);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolder);
            }
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
