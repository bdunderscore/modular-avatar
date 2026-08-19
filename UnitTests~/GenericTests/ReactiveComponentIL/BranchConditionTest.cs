using System.Collections;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnitTests.SharedInterfacesImpl;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class BranchConditionTest : ReactiveComponentILTestBase
    {
        [RCILTest]
        public IEnumerator TestBranchNodeConditions()
        {
            CreateSensor("true", out var motionTrue, out var testTrue);
            CreateSensor("false", out var motionFalse, out var testFalse);
            AddParameter("test", 0);
            
            var branch = new BranchNode("test");
            branch.OnGreaterEquals = motionTrue;
            branch.OnLessThan = motionFalse;
            
            BakeConditions(branch);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            yield return null;
            // Initially false
            Assert.IsTrue(testFalse());
            
            animator.SetFloat("test", 1);
            yield return null;
            Assert.IsTrue(testTrue());
        }
    }
}