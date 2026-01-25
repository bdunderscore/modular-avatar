using System.Collections;
using System.Diagnostics.CodeAnalysis;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnitTests.SharedInterfacesImpl;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    [SuppressMessage("ReSharper", "Unity.PreferAddressByIdToGraphicsParams")]
    public class PriorityNodeTest : ReactiveComponentILTestBase
    {
        private static ProxyCondition CreateBranchCondition(bool initialState, string parameterName)
        {
            return ProxyCondition.FromInner(initialState, (onFalse, onTrue) => new BranchNode(parameterName, onFalse, onTrue));
        }

        [RCILTest]
        public IEnumerator TestSingleConditionOn()
        {
            CreateSensor("motion0", out var motion0, out var test0);
            CreateSensor("default", out var defaultMotion, out var testDefault);
            AddParameter("cond0", 0);
            
            var priority = new PriorityNode();
            priority.DefaultMotion = defaultMotion;
            priority.Conditions.Add((
                CreateBranchCondition(false, "cond0"),
                motion0
            ));
            
            BakeConditions(priority);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            // First frame: condition evaluates
            yield return null;
            // Second frame: result is visible
            yield return null;
            
            // Initially cond0 is 0, so default should be active
            Assert.IsTrue(testDefault());
            Assert.IsFalse(test0());
            
            // Set condition to true
            animator.SetFloat("cond0", 1);
            yield return null;
            yield return null;
            
            // Now motion0 should be active
            Assert.IsTrue(test0());
            Assert.IsFalse(testDefault());
            
            // Set condition back to false
            animator.SetFloat("cond0", 0);
            yield return null;
            yield return null;
            
            // Default should be active again
            Assert.IsTrue(testDefault());
            Assert.IsFalse(test0());
        }

        [RCILTest]
        public IEnumerator TestTwoConditionsFirstTrue()
        {
            CreateSensor("motion0", out var motion0, out var test0);
            CreateSensor("motion1", out var motion1, out var test1);
            CreateSensor("default", out var defaultMotion, out var testDefault);
            AddParameter("cond0", 0);
            AddParameter("cond1", 0);
            
            var priority = new PriorityNode();
            priority.DefaultMotion = defaultMotion;
            priority.Conditions.Add((
                CreateBranchCondition(false, "cond0"),
                motion0
            ));
            priority.Conditions.Add((
                CreateBranchCondition(false, "cond1"),
                motion1
            ));
            
            BakeConditions(priority);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            yield return null;
            yield return null;
            
            // Both false - default is active
            Assert.IsTrue(testDefault());
            Assert.IsFalse(test0());
            Assert.IsFalse(test1());
            
            // Set cond0 to true
            animator.SetFloat("cond0", 1);
            yield return null;
            yield return null;
            
            // Only motion0 should be active (first true condition wins)
            Assert.IsTrue(test0());
            Assert.IsFalse(test1());
            Assert.IsFalse(testDefault());
            
            // Set cond1 to true as well
            animator.SetFloat("cond1", 1);
            yield return null;
            yield return null;
            
            // Still only motion0 (first condition wins)
            Assert.IsTrue(test0());
            Assert.IsFalse(test1());
            Assert.IsFalse(testDefault());
        }

        [RCILTest]
        public IEnumerator TestTwoConditionsSecondTrue()
        {
            CreateSensor("motion0", out var motion0, out var test0);
            CreateSensor("motion1", out var motion1, out var test1);
            CreateSensor("default", out var defaultMotion, out var testDefault);
            AddParameter("cond0", 0);
            AddParameter("cond1", 0);
            
            var priority = new PriorityNode();
            priority.DefaultMotion = defaultMotion;
            priority.Conditions.Add((
                CreateBranchCondition(false, "cond0"),
                motion0
            ));
            priority.Conditions.Add((
                CreateBranchCondition(false, "cond1"),
                motion1
            ));
            
            BakeConditions(priority);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            yield return null;
            yield return null;
            
            // Set only cond1 to true
            animator.SetFloat("cond1", 1);
            yield return null;
            yield return null;
            
            // Only motion1 should be active
            Assert.IsTrue(test1());
            Assert.IsFalse(test0());
            Assert.IsFalse(testDefault());
            
            // Set cond0 to true as well
            animator.SetFloat("cond0", 1);
            yield return null;
            yield return null;
            
            // Now motion0 wins (first condition)
            Assert.IsTrue(test0());
            Assert.IsFalse(test1());
            Assert.IsFalse(testDefault());
        }

        [RCILTest]
        public IEnumerator TestNineTeenConditions()
        {
            const int numConditions = 19;
            var motions = new MotionNode[numConditions];
            var testFuncs = new System.Func<bool>[numConditions];
            CreateSensor("default", out var defaultMotion, out var testDefault);
            
            // Create 19 conditions and sensors
            for (int i = 0; i < numConditions; i++)
            {
                CreateSensor($"motion{i}", out motions[i], out testFuncs[i]);
                AddParameter($"cond{i}", 0);
            }
            
            var priority = new PriorityNode();
            priority.DefaultMotion = defaultMotion;
            for (int i = 0; i < numConditions; i++)
            {
                int condIndex = i; // Capture for closure
                priority.Conditions.Add((
                    ProxyCondition.FromInner(false, (onFalse, onTrue) => new BranchNode($"cond{condIndex}", onFalse, onTrue)),
                    motions[i]
                ));
            }
            
            BakeConditions(priority);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            yield return null;
            yield return null;
            
            // All false - default is active
            Assert.IsTrue(testDefault());
            for (int i = 0; i < numConditions; i++)
            {
                Assert.IsFalse(testFuncs[i]());
            }
            
            // Enable first condition
            animator.SetFloat("cond0", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[0]());
            Assert.IsFalse(testDefault());
            for (int i = 1; i < numConditions; i++)
            {
                Assert.IsFalse(testFuncs[i]());
            }
            
            // Enable last condition too - first should still win
            animator.SetFloat("cond18", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[0]());
            Assert.IsFalse(testFuncs[18]());
            Assert.IsFalse(testDefault());
            
            // Disable first, enable last - last should now be active
            animator.SetFloat("cond0", 0);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[18]());
            Assert.IsFalse(testFuncs[0]());
            Assert.IsFalse(testDefault());
            
            // Enable middle condition - it should win over last
            animator.SetFloat("cond10", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[10]());
            Assert.IsFalse(testFuncs[18]());
            Assert.IsFalse(testDefault());
            
            // Disable all - default should be active
            animator.SetFloat("cond10", 0);
            animator.SetFloat("cond18", 0);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testDefault());
            for (int i = 0; i < numConditions; i++)
            {
                Assert.IsFalse(testFuncs[i]());
            }
        }

        [RCILTest]
        public IEnumerator TestTwentyFiveConditions()
        {
            const int numConditions = 25;
            var motions = new MotionNode[numConditions];
            var testFuncs = new System.Func<bool>[numConditions];
            CreateSensor("default", out var defaultMotion, out var testDefault);
            
            // Create 25 conditions and sensors
            for (int i = 0; i < numConditions; i++)
            {
                CreateSensor($"motion{i}", out motions[i], out testFuncs[i]);
                AddParameter($"cond{i}", 0);
            }
            
            var priority = new PriorityNode();
            priority.DefaultMotion = defaultMotion;
            for (int i = 0; i < numConditions; i++)
            {
                int condIndex = i; // Capture for closure
                priority.Conditions.Add((
                    ProxyCondition.FromInner(false, (onFalse, onTrue) => new BranchNode($"cond{condIndex}", onFalse, onTrue)),
                    motions[i]
                ));
            }
            
            BakeConditions(priority);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            yield return null;
            yield return null;
            
            // All false - default is active
            Assert.IsTrue(testDefault());
            for (int i = 0; i < numConditions; i++)
            {
                Assert.IsFalse(testFuncs[i]());
            }
            
            // Enable condition 20 (in the sub-node range)
            animator.SetFloat("cond20", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[20]());
            Assert.IsFalse(testDefault());
            for (int i = 0; i < numConditions; i++)
            {
                if (i != 20) Assert.IsFalse(testFuncs[i]());
            }
            
            // Enable condition 0 - it should win even though it's in a different sub-node
            animator.SetFloat("cond0", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[0]());
            Assert.IsFalse(testFuncs[20]());
            Assert.IsFalse(testDefault());
            
            // Disable condition 0, enable last condition - last should be active
            animator.SetFloat("cond0", 0);
            animator.SetFloat("cond20", 0);
            animator.SetFloat("cond24", 1);
            yield return null;
            yield return null;
            
            
            Assert.IsTrue(testFuncs[24]());
            Assert.IsFalse(testFuncs[20]());
            Assert.IsFalse(testDefault());
            
            // Enable condition 23 - it should win over 24 (earlier in list)
            animator.SetFloat("cond23", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[23]());
            Assert.IsFalse(testFuncs[24]());
            Assert.IsFalse(testDefault());
            
            // Enable condition 19 (last in main node) - it should win
            animator.SetFloat("cond19", 1);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testFuncs[19]());
            Assert.IsFalse(testFuncs[23]());
            Assert.IsFalse(testFuncs[24]());
            Assert.IsFalse(testDefault());
            
            // Test disabling all
            animator.SetFloat("cond19", 0);
            animator.SetFloat("cond20", 0);
            animator.SetFloat("cond23", 0);
            animator.SetFloat("cond24", 0);
            yield return null;
            yield return null;
            
            Assert.IsTrue(testDefault());
            for (int i = 0; i < numConditions; i++)
            {
                Assert.IsFalse(testFuncs[i]());
            }
        }

        [RCILTest]
        public IEnumerator TestInitialStateFrame()
        {
            CreateSensor("motion0", out var motion0, out var test0);
            CreateSensor("motion1", out var motion1, out var test1);
            CreateSensor("default", out var defaultMotion, out var testDefault);
            AddParameter("cond0", 0);
            AddParameter("cond1", 0);
            
            var priority = new PriorityNode();
            priority.DefaultMotion = defaultMotion;
            // Condition 0 has initialState=true
            priority.Conditions.Add((
                CreateBranchCondition(true, "cond0"),
                motion0
            ));
            // Condition 1 has initialState=false, parameter is 0
            priority.Conditions.Add((
                CreateBranchCondition(false, "cond1"),
                motion1
            ));
            
            BakeConditions(priority);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            animator.SetFloat("cond0", 1);
            
            // First frame: condition evaluates
            Debug.Log("FIRST FRAME: value " + animator.GetFloat("$$MA/RC/PriorityNode$0"));
            yield return null;
            
            // On the first frame (before we've computed the sum), we should not allow a state where
            // the animated objects are in the wrong state.
            Assert.IsTrue(test0(), "On initial frame, motion0 should be active because cond0 has initialState=true");
            Assert.IsFalse(test1());
            Assert.IsFalse(testDefault());
            
            // Second frame: result is visible
            yield return null;
            Assert.IsTrue(test0(), "Once initialized, motion0 should still be active");
            Assert.IsFalse(test1());
            Assert.IsFalse(testDefault());
            
            // Now set the parameters to opposite of their initial states
            // cond0 initialState=true, set to 0 (false)
            // cond1 initialState=false, set to 1 (true)
            animator.SetFloat("cond0", 0);
            animator.SetFloat("cond1", 1);
            yield return null;
            yield return null;
            
            // On the second evaluation frame, parameters should be used:
            // cond0 parameter is now 0 (false)
            // cond1 parameter is now 1 (true) - first true condition wins
            Assert.IsTrue(test1(), "After first frame, cond1 should be active because its parameter is 1");
            Assert.IsFalse(test0());
            Assert.IsFalse(testDefault());
            
            // Set cond1 back to false and cond0 to true (parameter value)
            animator.SetFloat("cond0", 1);
            animator.SetFloat("cond1", 0);
            yield return null;
            yield return null;
            
            // Now motion0 should be active based on parameters
            Assert.IsTrue(test0(), "When cond0 parameter is 1, motion0 should be active");
            Assert.IsFalse(test1());
            Assert.IsFalse(testDefault());
        }
    }
}

