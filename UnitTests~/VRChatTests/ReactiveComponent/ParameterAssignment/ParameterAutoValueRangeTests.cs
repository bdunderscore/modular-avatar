#if MA_VRCSDK3_AVATARS

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
    public class ParameterAutoValueRangeTests : TestBase
    {
        [Test]
        public void AutoAssignedIntValues_StayWithinVrcRange_AndEmitErrorWhenExhausted()
        {
            var root = CreateRoot("Root");
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            descriptor.expressionParameters.parameters = System.Array.Empty<VRCExpressionParameters.Parameter>();

            const int itemCount = 260; // More values than the 0-255 Int parameter range allows
            for (var i = 0; i < itemCount; i++)
            {
                var obj = CreateChild(root, i.ToString());
                var mami = obj.AddComponent<ModularAvatarMenuItem>();
                mami.Control = new VRCExpressionsMenu.Control()
                {
                    name = obj.name,
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new() { name = "Test" },
                };
                mami.automaticValue = true;
            }

            var context = new nadena.dev.ndmf.BuildContext(root, null);

            var errors = nadena.dev.ndmf.ErrorReport.CaptureErrors(() =>
            {
                new ParameterAssignerPass().TestExecute(context);
            });

            var error = errors.Select(e => e.TheError as nadena.dev.ndmf.SimpleError)
                .FirstOrDefault(se => se?.TitleKey == "error.parameter_assigner.value_out_of_range");
            Assert.IsNotNull(error,
                "Expected a BuildReport error when auto-assigned values exceed the VRChat Int parameter range.");
            Assert.AreEqual(nadena.dev.ndmf.ErrorSeverity.Error, error.Severity);

            var values = root.GetComponentsInChildren<ModularAvatarMenuItem>()
                .Select(m => (int)m.Control.value)
                .ToList();

            Assert.IsTrue(values.All(v => v >= 0 && v <= 255),
                "Auto-assigned values must stay within the VRChat Int parameter range (0-255).");

            var param = descriptor.expressionParameters.parameters.Single();
            Assert.AreEqual(VRCExpressionParameters.ValueType.Int, param.valueType);
        }
    }
}

#endif
