#if MA_VRCSDK3_AVATARS

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace UnitTests.ReactiveComponent.ParameterAssignment
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