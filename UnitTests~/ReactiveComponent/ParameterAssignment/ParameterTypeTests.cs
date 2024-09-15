using System;
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
    public class ParameterTypeTests : TestBase
    {
        [Test]
        public void BoolTest()
        {
            Test(new[] { 0.0f }, VRCExpressionParameters.ValueType.Bool);
            Test(new[] { 1.0f }, VRCExpressionParameters.ValueType.Bool);

            Test(new[] { 0.0f, 1.0f }, VRCExpressionParameters.ValueType.Bool);
            Test(new[] { 1.0f, 0.0f }, VRCExpressionParameters.ValueType.Bool);
        }

        [Test]
        public void IntTest()
        {
            Test(new[] { 2.0f }, VRCExpressionParameters.ValueType.Int);
            Test(new[] { 3.0f }, VRCExpressionParameters.ValueType.Int);

            Test(new[] { 0.0f, 1.0f, 2.0f }, VRCExpressionParameters.ValueType.Int);
            Test(new[] { 2.0f, 1.0f, 0.0f }, VRCExpressionParameters.ValueType.Int);

            Test(new[] { 253.0f, 254.0f, 255.0f }, VRCExpressionParameters.ValueType.Int);
            Test(new[] { 255.0f, 254.0f, 253.0f }, VRCExpressionParameters.ValueType.Int);
        }

        [Test]
        public void FloatTest()
        {
            Test(new[] { -1.0f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { -0.1f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { 0.1f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { 0.9f }, VRCExpressionParameters.ValueType.Float);

            Test(new[] { -1.0f, 0.0f, 1.0f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { 1.0f, 0.0f, -1.0f }, VRCExpressionParameters.ValueType.Float);

            Test(new[] { -0.1f, 0.0f, 0.1f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { 0.1f, 0.0f, -0.1f }, VRCExpressionParameters.ValueType.Float);

            Test(new[] { 0.0f, 0.1f, 0.2f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { 0.2f, 0.1f, 0.0f }, VRCExpressionParameters.ValueType.Float);

            Test(new[] { 0.7f, 0.8f, 0.9f }, VRCExpressionParameters.ValueType.Float);
            Test(new[] { 0.9f, 0.8f, 0.7f }, VRCExpressionParameters.ValueType.Float);
        }

        private void Test(float[] values, VRCExpressionParameters.ValueType expected)
        {
            var root = CreateRoot("Root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            descriptor.expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();

            for (var i = 0; i < values.Length; i++)
            {
                var obj = CreateChild(root, i.ToString());
                var mami = obj.AddComponent<ModularAvatarMenuItem>();
                mami.Control = new VRCExpressionsMenu.Control()
                {
                    name = obj.name,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    value = values[i],
                    parameter = new() { name = "Test" },
                };
            }

            var context = new nadena.dev.ndmf.BuildContext(root, null);
            new ParameterAssignerPass().TestExecute(context);

            Assert.AreEqual(expected, descriptor.expressionParameters.parameters.Single().valueType);
        }
    }
}