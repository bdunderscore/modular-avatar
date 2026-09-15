using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnityEngine.TestTools;

namespace UnitTests._PlayModeTests.ReactiveComponents
{
    public class OneDBlendTreeTestStub : TestBase
    {
        // https://discussions.unity.com/t/testcasesource-compatible-with-unitytest/817168
        public static TestCaseData[] cases = ReactiveComponentILTestSharedBase.TestNames("OneDBlendTreeTest")
            .Select(name => new TestCaseData(name).Returns(null))
            .ToArray();

        [UnityTest]
        [TestCaseSource(nameof(cases))]
        public IEnumerator Runner(string testName)
        {
            return ReactiveComponentILTestSharedBase.InvokeTest("OneDBlendTreeTest", testName);
        }
    }
}
