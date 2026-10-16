using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using nadena.dev.modular_avatar.test.SharedInterfaces;
using UnityEngine.TestTools;

namespace nadena.dev.modular_avatar.test._PlayModeTests.ReactiveComponents
{
    public class BranchConditionTestStub : TestBase
    {
        // https://discussions.unity.com/t/testcasesource-compatible-with-unitytest/817168
        public static TestCaseData[] cases = ReactiveComponentILTestSharedBase.TestNames("BranchConditionTest")
            .Select(name => new TestCaseData(name).Returns(null))
            .ToArray();
        
        [UnityTest]
        [TestCaseSource(nameof(cases))]
        public IEnumerator Runner(string testName)
        {
            return ReactiveComponentILTestSharedBase.InvokeTest("BranchConditionTest", testName);
        }
    }
}