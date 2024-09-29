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