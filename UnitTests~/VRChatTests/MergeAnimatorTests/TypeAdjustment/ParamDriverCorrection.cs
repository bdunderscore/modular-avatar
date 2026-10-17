#if MA_VRCSDK3_AVATARS

using System.Collections;
using System.Collections.Generic;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace nadena.dev.modular_avatar.test.Tests
{
    public class ParamDriverCorrection : TestBase
    {
        [Test]
        public void WhenParameterTypesChange_ParamDriversAreCorrected(
            [Values("BoolThenFloat", "FloatThenBool")]
            string variant
        )
        {
            var prefab = CreatePrefab("ParamDriverCorrection_" + variant + ".prefab");
    
            AvatarProcessor.ProcessAvatar(prefab);
    
            var layer = findFxLayer(prefab, "bool");
            var paramDriver = (VRCAvatarParameterDriver) layer.stateMachine.defaultState.behaviours[0];
            
            Assert.AreEqual(2, paramDriver.parameters.Count);
            var tmp = paramDriver.parameters[0].name;
            Assert.AreNotEqual("xyz", tmp);
            Assert.AreEqual(VRC_AvatarParameterDriver.ChangeType.Random, paramDriver.parameters[0].type);
            
            Assert.AreEqual("xyz", paramDriver.parameters[1].name);
            Assert.AreEqual(tmp, paramDriver.parameters[1].source);
            Assert.AreEqual(VRC_AvatarParameterDriver.ChangeType.Copy, paramDriver.parameters[1].type);
        }
    }
}

#endif