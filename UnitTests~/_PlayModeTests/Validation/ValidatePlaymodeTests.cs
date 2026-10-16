using System;
using System.Collections;
using NUnit.Framework;
using nadena.dev.modular_avatar.test.SharedInterfaces;
using UnityEngine;
using UnityEngine.TestTools;

namespace nadena.dev.modular_avatar.test._PlayModeTests
{
    public class InfraValidationTest : TestBase
    {
        [UnityTest]
        public IEnumerator RuntimeTest()
        {
            var avatar = CreatePrefab("ValidatePlaymodeTests.prefab");
            ITestSupport.Instance.ProcessAvatar(avatar);
            ITestSupport.Instance.ActivateFX(avatar);
    
            var cube = avatar.transform.Find("Anim/Cube").gameObject;
            
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(cube.activeSelf);
        }
    }
}