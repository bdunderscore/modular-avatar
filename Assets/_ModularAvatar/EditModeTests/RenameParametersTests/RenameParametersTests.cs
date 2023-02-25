using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.RenameParametersTests
{
    public class RenameParametersTests : TestBase
    {
        [Test]
        public void RenameParametersRenamesParamDrivers()
        {
            var prefab = CreatePrefab("RenameParametersRenamesParamDrivers.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var layer = findFxLayer(prefab, "test");
            CheckDriver(layer.stateMachine.behaviours[0] as VRCAvatarParameterDriver);

            var state = FindStateInLayer(layer, "s");
            CheckDriver(state.behaviours[0] as VRCAvatarParameterDriver);

            var subStateMachine = layer.stateMachine.stateMachines[0].stateMachine;
            CheckDriver(subStateMachine.behaviours[0] as VRCAvatarParameterDriver);

            var subState = FindStateInLayer(layer, "s");
            CheckDriver(subState.behaviours[0] as VRCAvatarParameterDriver);

            void CheckDriver(VRCAvatarParameterDriver driver)
            {
                Assert.AreEqual("param_new", driver.parameters[0].name);
            }
        }
    }
}