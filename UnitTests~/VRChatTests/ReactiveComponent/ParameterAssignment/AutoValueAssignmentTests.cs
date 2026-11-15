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
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            var menu = descriptor.expressionsMenu.controls.First(c => c.name == "child");
            Assert.AreEqual(1, menu.value);
            Assert.AreEqual(isDefault ? 1 : 0, descriptor.expressionParameters.parameters.Single(p => p.name == "foo").defaultValue);
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

        [TestCase(VRCExpressionParameters.ValueType.Bool, 0, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 0, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 1, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 1, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 1, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 1, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0.5f, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, false, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 1, true, 1, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 1, false, 2, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 2, true, 2, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 2, false, 1, 2)]
        public void SingletonAutomaticValuesRespectDeclaredExternalParameterDefaults(
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
            Assert.AreEqual(isDefault, menuItem.isDefault);
            var declaredParameter = descriptor.expressionParameters.parameters.Single();
            Assert.AreEqual(valueType, declaredParameter.valueType);
            Assert.AreEqual(expectedParameterDefault, declaredParameter.defaultValue);
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, 0, true, 0, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 0, false, 0, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 1, true, 1, 0, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 1, false, 1, 0, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, true, 0, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, false, 0, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 1, true, 1, 0, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 1, false, 1, 0, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, true, 0, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, false, 0, 1, 0)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 1, true, 1, 2, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 1, false, 1, 2, 1)]
        public void TwoAutomaticTogglesRespectDeclaredExternalParameterDefault(
            VRCExpressionParameters.ValueType valueType,
            float defaultValue,
            bool selectedMenuFirst,
            float expectedSelectedValue,
            float expectedUnselectedValue,
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

            ModularAvatarMenuItem selectedMenuItem = null;
            ModularAvatarMenuItem unselectedMenuItem = null;
            foreach (var selected in new[] { selectedMenuFirst, !selectedMenuFirst })
            {
                var menu = CreateChild(root, selected ? "selected" : "unselected");
                var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
                menuItem.automaticValue = true;
                menuItem.isDefault = selected;
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
                };

                if (selected) selectedMenuItem = menuItem;
                else unselectedMenuItem = menuItem;
            }

            var context = new nadena.dev.ndmf.BuildContext(root, null);
            new ParameterAssignerPass().TestExecute(context);

            Assert.AreEqual(expectedSelectedValue, selectedMenuItem.Control.value);
            Assert.IsTrue(selectedMenuItem.isDefault);
            Assert.AreEqual(expectedUnselectedValue, unselectedMenuItem.Control.value);
            Assert.IsFalse(unselectedMenuItem.isDefault);
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
        
        [TestCase(VRCExpressionParameters.ValueType.Bool, true)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, true)]
        [TestCase(VRCExpressionParameters.ValueType.Int, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, false)]
        public void SharedExternalParameterDefaultFollowsAutomaticSelection(
            VRCExpressionParameters.ValueType valueType,
            bool automaticMenuFirst
        )
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
                        valueType = valueType,
                        defaultValue = 0,
                    }
                };
                AssetDatabase.CreateAsset(source, assetPath);

                var root = CreateRoot("root");
                var descriptor = root.GetComponent<VRCAvatarDescriptor>();
                descriptor.expressionParameters = source;

                ModularAvatarMenuItem automaticMenuItem = null;
                ModularAvatarMenuItem manualMenuItem = null;
                foreach (var automatic in new[] { automaticMenuFirst, !automaticMenuFirst })
                {
                    var menu = CreateChild(root, automatic ? "automatic" : "manual");
                    var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
                    menuItem.automaticValue = automatic;
                    menuItem.isDefault = automatic;
                    menuItem.Control = new VRCExpressionsMenu.Control
                    {
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        value = 0,
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = "outfit" }
                    };

                    if (automatic) automaticMenuItem = menuItem;
                    else manualMenuItem = menuItem;
                }

                var context = new nadena.dev.ndmf.BuildContext(root, null);
                new ParameterAssignerPass().TestExecute(context);
                TrackObject(descriptor.expressionParameters);

                Assert.AreEqual(0, source.parameters.Single().defaultValue);
                Assert.AreEqual(1, automaticMenuItem.Control.value);
                Assert.IsTrue(automaticMenuItem.isDefault);
                Assert.AreEqual(0, manualMenuItem.Control.value);
                Assert.IsFalse(manualMenuItem.isDefault);
                Assert.AreEqual(1, descriptor.expressionParameters.parameters.Single().defaultValue);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolder);
            }
        }


        [TestCase(ParameterSyncType.Bool, VRCExpressionParameters.ValueType.Bool, 0)]
        [TestCase(ParameterSyncType.Float, VRCExpressionParameters.ValueType.Float, 0)]
        [TestCase(ParameterSyncType.Int, VRCExpressionParameters.ValueType.Int, 2)]
        public void ImplicitMAParameterDefaultTracksSelectedManualMenuValue(
            ParameterSyncType syncType,
            VRCExpressionParameters.ValueType expectedValueType,
            float expectedAutomaticValue
        )
        {
            var result = ProcessSharedParameterMenus(syncType, parameterAlreadyExists: false, explicitDefault: null);

            Assert.AreEqual(1, result.ManualMenu.Control.value);
            Assert.IsTrue(result.ManualMenu.isDefault);
            Assert.AreEqual(expectedAutomaticValue, result.AutomaticMenu.Control.value);
            Assert.IsFalse(result.AutomaticMenu.isDefault);
            Assert.AreEqual(expectedValueType, result.Parameter.valueType);
            Assert.AreEqual(1, result.Parameter.defaultValue);
        }

        [TestCase(ParameterSyncType.Bool, VRCExpressionParameters.ValueType.Bool, 0)]
        [TestCase(ParameterSyncType.Float, VRCExpressionParameters.ValueType.Float, 0)]
        [TestCase(ParameterSyncType.Int, VRCExpressionParameters.ValueType.Int, 2)]
        public void ExistingExpressionParameterDefaultIsNotOverriddenBySelectedManualMenuValue(
            ParameterSyncType syncType,
            VRCExpressionParameters.ValueType expectedValueType,
            float expectedAutomaticValue
        )
        {
            var result = ProcessSharedParameterMenus(syncType, parameterAlreadyExists: true, explicitDefault: null);

            Assert.AreEqual(1, result.ManualMenu.Control.value);
            Assert.AreEqual(expectedAutomaticValue, result.AutomaticMenu.Control.value);
            Assert.AreEqual(expectedValueType, result.Parameter.valueType);
            Assert.AreEqual(0, result.Parameter.defaultValue);
        }

        [Test]
        public void PersistentExpressionParameterZeroDefaultIsNotOverriddenBySelectedManualMenuValue()
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
                    }
                };
                AssetDatabase.CreateAsset(source, assetPath);

                var root = CreateRoot("root");
                var descriptor = root.GetComponent<VRCAvatarDescriptor>();
                descriptor.expressionParameters = source;
                var parameterHost = CreateChild(root, "parameter host");
                parameterHost.AddComponent<ModularAvatarParameters>().parameters = new()
                {
                    new ParameterConfig
                    {
                        nameOrPrefix = "outfit",
                        syncType = ParameterSyncType.Bool,
                    }
                };
                var (manualMenu, automaticMenu) = AddSharedParameterMenus(parameterHost, "outfit");

                AvatarProcessor.ProcessAvatar(root);

                Assert.AreEqual(0, source.parameters.Single().defaultValue);
                Assert.AreEqual(1, manualMenu.Control.value);
                Assert.AreEqual(0, automaticMenu.Control.value);
                Assert.AreEqual(0, descriptor.expressionParameters.parameters.Single().defaultValue);
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetFolder);
            }
        }

        [TestCase(ParameterSyncType.Bool, VRCExpressionParameters.ValueType.Bool, 0)]
        [TestCase(ParameterSyncType.Float, VRCExpressionParameters.ValueType.Float, 0)]
        [TestCase(ParameterSyncType.Int, VRCExpressionParameters.ValueType.Int, 2)]
        public void ExplicitZeroMAParameterDefaultIsNotOverriddenBySelectedManualMenuValue(
            ParameterSyncType syncType,
            VRCExpressionParameters.ValueType expectedValueType,
            float expectedAutomaticValue
        )
        {
            var result = ProcessSharedParameterMenus(syncType, parameterAlreadyExists: false, explicitDefault: 0);

            Assert.AreEqual(1, result.ManualMenu.Control.value);
            Assert.AreEqual(expectedAutomaticValue, result.AutomaticMenu.Control.value);
            Assert.AreEqual(expectedValueType, result.Parameter.valueType);
            Assert.AreEqual(0, result.Parameter.defaultValue);
        }

        [Test]
        public void ImplicitInternalMAParameterDefaultTracksSelectedManualMenuValueAfterRemapping()
        {
            var root = CreateRoot("root");
            var parameterHost = CreateChild(root, "parameter host");
            parameterHost.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new ParameterConfig
                {
                    nameOrPrefix = "outfit",
                    syncType = ParameterSyncType.Bool,
                    internalParameter = true,
                }
            };

            var (manualMenu, automaticMenu) = AddSharedParameterMenus(parameterHost, "outfit");
            AvatarProcessor.ProcessAvatar(root);

            var assignedName = manualMenu.Control.parameter.name;
            Assert.AreNotEqual("outfit", assignedName);
            Assert.AreEqual(assignedName, automaticMenu.Control.parameter.name);
            Assert.AreEqual(1, manualMenu.Control.value);
            Assert.AreEqual(0, automaticMenu.Control.value);
            Assert.AreEqual(1, root.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters
                .Single(p => p.name == assignedName).defaultValue);
        }

        [Test]
        public void ExplicitMAParameterDefaultWinsWhenMergedWithImplicitDefault()
        {
            var root = CreateRoot("root");
            var implicitParameterHost = CreateChild(root, "implicit parameter host");
            implicitParameterHost.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new ParameterConfig
                {
                    nameOrPrefix = "outfit",
                    syncType = ParameterSyncType.Int,
                }
            };
            var explicitParameterHost = CreateChild(implicitParameterHost, "explicit parameter host");
            explicitParameterHost.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new ParameterConfig
                {
                    nameOrPrefix = "outfit",
                    syncType = ParameterSyncType.Int,
                    defaultValue = 2,
                    hasExplicitDefaultValue = true,
                }
            };

            var (manualMenu, automaticMenu) = AddSharedParameterMenus(explicitParameterHost, "outfit");
            AvatarProcessor.ProcessAvatar(root);

            Assert.AreEqual(1, manualMenu.Control.value);
            Assert.AreEqual(3, automaticMenu.Control.value);
            Assert.AreEqual(2, root.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters
                .Single(p => p.name == "outfit").defaultValue);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void UnnamedSingletonAutomaticMenuCreatesBoolParameter(bool isDefault, float expectedDefault)
        {
            var result = ProcessGeneratedMenuValues(
                null,
                (true, isDefault, 0)
            );

            Assert.AreEqual(VRCExpressionParameters.ValueType.Bool, result.Parameter.valueType);
            Assert.AreEqual(1, result.MenuItems.Single().Control.value);
            Assert.AreEqual(expectedDefault, result.Parameter.defaultValue);
            Assert.AreEqual(result.Parameter.name, result.MenuItems.Single().Control.parameter.name);
            Assert.That(result.Parameter.name, Does.StartWith(ParameterAssignerPass.AUTOMATIC_PARAMETER_PREFIX));
        }

        [TestCase(true, false, VRCExpressionParameters.ValueType.Bool, 0, 1)]
        [TestCase(false, true, VRCExpressionParameters.ValueType.Bool, 1, 0)]
        [TestCase(false, false, VRCExpressionParameters.ValueType.Int, 1, 2)]
        public void TwoAutomaticMenusAllocateGeneratedParameterValues(
            bool firstIsDefault,
            bool secondIsDefault,
            VRCExpressionParameters.ValueType expectedType,
            float expectedFirstValue,
            float expectedSecondValue
        )
        {
            var result = ProcessGeneratedMenuValues(
                "outfit",
                (true, firstIsDefault, 0),
                (true, secondIsDefault, 0)
            );

            AssertGeneratedParameter(
                result,
                expectedType,
                0,
                expectedFirstValue,
                expectedSecondValue
            );
        }

        [TestCase(true, 1, 0, 2, 0)]
        [TestCase(false, 1, 2, 3, 0)]
        public void ThreeAutomaticMenusAllocateGeneratedIntParameterValues(
            bool hasSelectedMenu,
            float expectedFirstValue,
            float expectedSecondValue,
            float expectedThirdValue,
            float expectedDefault
        )
        {
            var result = ProcessGeneratedMenuValues(
                "outfit",
                (true, false, 0),
                (true, hasSelectedMenu, 0),
                (true, false, 0)
            );

            AssertGeneratedParameter(
                result,
                VRCExpressionParameters.ValueType.Int,
                expectedDefault,
                expectedFirstValue,
                expectedSecondValue,
                expectedThirdValue
            );
        }

        [Test]
        public void ManualDefaultOneAndAutomaticMenuCreateGeneratedIntParameter()
        {
            var result = ProcessGeneratedMenuValues(
                "outfit",
                (false, true, 1),
                (true, false, 0)
            );

            AssertGeneratedParameter(result, VRCExpressionParameters.ValueType.Int, 1, 1, 2);
        }

        [Test]
        public void ManualFractionalMenuAndAutomaticDefaultCreateGeneratedFloatParameter()
        {
            var result = ProcessGeneratedMenuValues(
                "outfit",
                (false, false, 0.5f),
                (true, true, 0)
            );

            AssertGeneratedParameter(result, VRCExpressionParameters.ValueType.Float, 1, 0.5f, 1);
        }

        private (
            List<ModularAvatarMenuItem> MenuItems,
            VRCExpressionParameters.Parameter Parameter
        ) ProcessGeneratedMenuValues(
            string? parameterName,
            params (bool Automatic, bool IsDefault, float Value)[] controls
        )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            var menuItems = new List<ModularAvatarMenuItem>();
            foreach (var (automatic, isDefault, value) in controls)
            {
                var menu = CreateChild(root, "menu" + menuItems.Count);
                var menuItem = menu.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    value = value,
                    parameter = string.IsNullOrEmpty(parameterName)
                        ? new VRCExpressionsMenu.Control.Parameter()
                        : new VRCExpressionsMenu.Control.Parameter { name = parameterName },
                };
                menuItem.automaticValue = automatic;
                menuItem.isDefault = isDefault;
                if (string.IsNullOrEmpty(parameterName))
                {
                    var target = CreateChild(root, "target" + menuItems.Count);
                    menu.AddComponent<ModularAvatarObjectToggle>().Objects.Add(new ToggledObject
                    {
                        Object = new AvatarObjectReference(target),
                        Active = false
                    });
                }
                menu.AddComponent<ModularAvatarMenuInstaller>();
                menuItems.Add(menuItem);
            }

            AvatarProcessor.ProcessAvatar(root);

            return (menuItems, descriptor.expressionParameters.parameters.Single());
        }

        private static void AssertGeneratedParameter(
            (List<ModularAvatarMenuItem> MenuItems, VRCExpressionParameters.Parameter Parameter) result,
            VRCExpressionParameters.ValueType expectedType,
            float expectedDefault,
            params float[] expectedValues
        )
        {
            Assert.AreEqual("outfit", result.Parameter.name);
            Assert.AreEqual(expectedType, result.Parameter.valueType);
            Assert.AreEqual(expectedDefault, result.Parameter.defaultValue);
            CollectionAssert.AreEqual(expectedValues, result.MenuItems.Select(item => item.Control.value));
            CollectionAssert.AreEqual(
                Enumerable.Repeat("outfit", result.MenuItems.Count),
                result.MenuItems.Select(item => item.Control.parameter.name)
            );
        }

        private (ModularAvatarMenuItem ManualMenu, ModularAvatarMenuItem AutomaticMenu, VRCExpressionParameters.Parameter Parameter)
            ProcessSharedParameterMenus(
                ParameterSyncType syncType,
                bool parameterAlreadyExists,
                float? explicitDefault
            )
        {
            var root = CreateRoot("root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            descriptor.expressionParameters.parameters = parameterAlreadyExists
                ? new[]
                {
                    new VRCExpressionParameters.Parameter
                    {
                        name = "outfit",
                        valueType = ToExpressionParameterType(syncType),
                        defaultValue = 0,
                    }
                }
                : Array.Empty<VRCExpressionParameters.Parameter>();

            var parameterHost = CreateChild(root, "parameter host");
            var config = new ParameterConfig
            {
                nameOrPrefix = "outfit",
                syncType = syncType,
            };
            if (explicitDefault.HasValue)
            {
                config.defaultValue = explicitDefault.Value;
                config.hasExplicitDefaultValue = true;
            }

            parameterHost.AddComponent<ModularAvatarParameters>().parameters = new() { config };
            var (manualMenu, automaticMenu) = AddSharedParameterMenus(parameterHost, "outfit");

            AvatarProcessor.ProcessAvatar(root);

            return (
                manualMenu,
                automaticMenu,
                descriptor.expressionParameters.parameters.Single(p => p.name == "outfit")
            );
        }

        private (ModularAvatarMenuItem ManualMenu, ModularAvatarMenuItem AutomaticMenu)
            AddSharedParameterMenus(GameObject parent, string parameterName)
        {
            var manual = CreateChild(parent, "manual").AddComponent<ModularAvatarMenuItem>();
            manual.Control = new VRCExpressionsMenu.Control
            {
                name = "manual",
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                value = 1,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName },
            };
            manual.isDefault = true;

            var automatic = CreateChild(parent, "automatic").AddComponent<ModularAvatarMenuItem>();
            automatic.Control = new VRCExpressionsMenu.Control
            {
                name = "automatic",
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName },
            };
            automatic.automaticValue = true;

            parent.AddComponent<ModularAvatarMenuInstaller>();
            return (manual, automatic);
        }

        private static VRCExpressionParameters.ValueType ToExpressionParameterType(ParameterSyncType syncType)
        {
            return syncType switch
            {
                ParameterSyncType.Bool => VRCExpressionParameters.ValueType.Bool,
                ParameterSyncType.Int => VRCExpressionParameters.ValueType.Int,
                _ => VRCExpressionParameters.ValueType.Float,
            };
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
