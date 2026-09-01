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

        [RCILTest]
        public IEnumerator TestBranchNodeGreaterConditionIsStrictAtThreshold()
        {
            const float threshold = 0.5f;
            CreateSensor("true", out var motionTrue, out var testTrue);
            CreateSensor("false", out var motionFalse, out var testFalse);
            AddParameter("test", threshold);

            var branch = new BranchNode("test")
            {
                Threshold = threshold,
                OnGreaterEquals = motionTrue,
                OnLessThan = motionFalse
            };

            BakeConditions(branch);
            Assert.IsNotNull(animator.runtimeAnimatorController);

            yield return null;

            Assert.IsTrue(testFalse(), "A value equal to the threshold must take the false branch");
            Assert.IsFalse(testTrue(), "The strict greater-than branch must not activate at equality");
        }

        [RCILTest]
        public IEnumerator TestBranchNodeGreaterConditionActivatesOneUlpAboveThreshold()
        {
            const float threshold = 0.5f;
            CreateSensor("true", out var motionTrue, out var testTrue);
            CreateSensor("false", out var motionFalse, out var testFalse);
            AddParameter("test", threshold.NextLargest());

            var branch = new BranchNode("test")
            {
                Threshold = threshold,
                OnGreaterEquals = motionTrue,
                OnLessThan = motionFalse
            };

            BakeConditions(branch);
            Assert.IsNotNull(animator.runtimeAnimatorController);

            yield return null;

            Assert.IsTrue(testTrue(), "The strict greater-than branch must activate one ULP above the threshold");
            Assert.IsFalse(testFalse(), "One ULP above the threshold must not take the false branch");
        }
    }
}
