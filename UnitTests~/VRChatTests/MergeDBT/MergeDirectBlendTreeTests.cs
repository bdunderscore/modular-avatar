#if MA_VRCSDK3_AVATARS

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.test.Tests
{
    public class MergeDirectBlendTreeTests : TestBase
    {
        [Test]
        public void DoesNotOverwriteBaseParameters()
        {
            var prefab = CreatePrefab("DBT_DoesNotOverwriteParameters.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
    
            var fx = FindFxController(prefab);
            var ac = (AnimatorController)fx.animatorController;
            var parameters = ac.parameters.ToImmutableDictionary(i => i.name, i => i.defaultFloat);
            
            Assert.AreEqual(123, parameters["ABC"]);
            Assert.AreEqual(0, parameters["DEF"]);
        }
    }
}

#endif