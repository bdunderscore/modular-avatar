using System;
using System.Collections;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTests._PlayModeTests
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