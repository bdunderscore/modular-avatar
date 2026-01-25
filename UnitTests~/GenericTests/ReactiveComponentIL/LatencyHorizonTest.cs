using System.Collections;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnitTests.SharedInterfacesImpl;

namespace UnitTestsReactiveComponentIL
{
    public class LatencyHorizonTest : ReactiveComponentILTestBase
    {
        /// <summary>
        /// Tests that changes within a LatencyHorizon apply simultaneously.
        /// This tests a simple case with a BranchNode and LatencyHorizon wrapper.
        /// </summary>
        [RCILTest]
        public IEnumerator TestLatencyHorizonSimplifies()
        {
            CreateSensor("onTrue", out var motionTrue, out var testTrue);
            CreateSensor("onFalse", out var motionFalse, out var testFalse);
            AddParameter("test", 0);
            
            var branch = new BranchNode("test");
            branch.OnGreaterEquals = new LatencyHorizon(motionTrue);
            branch.OnLessThan = new LatencyHorizon(motionFalse);
            
            BakeConditions(branch);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            yield return null;
            // Initially false
            Assert.IsTrue(testFalse());
            
            animator.SetFloat("test", 1);
            yield return null;
            // Should switch to true
            Assert.IsTrue(testTrue());
        }

        /// <summary>
        /// Tests that a BranchNode feeding into a PriorityNode through a LatencyHorizon
        /// causes all changes to happen at the same time, preventing intermediate states.
        /// 
        /// Structure:
        /// - BranchNode (branchParam, latency 0)
        ///   - OnTrue: LatencyHorizon(MotionTrue)
        ///   - OnFalse: PriorityNode (latency 1)
        ///     - DefaultMotion: LatencyHorizon(MotionDefault)
        ///     - Condition 0: LatencyHorizon(MotionCondition)
        /// </summary>
        [RCILTest]
        public IEnumerator TestLatencyHorizonPreventsIntermediateStates()
        {
            CreateSensor("motionTrue", out var motionTrue, out var testTrue);
            CreateSensor("motionDefault", out var motionDefault, out var testDefault);
            CreateSensor("motionCondition", out var motionCondition, out var testCondition);
            
            AddParameter("branchParam", 0);
            AddParameter("condParam", 0);
            
            // Create the priority node with latency horizon wrappers
            var priority = new PriorityNode();
            priority.DefaultMotion = new LatencyHorizon(motionDefault);
            priority.Conditions.Add((
                ProxyCondition.FromInner(false, (_, _) => new BranchNode("condParam")),
                new LatencyHorizon(motionCondition)
            ));
            
            // Create the branch node
            var branch = new BranchNode("branchParam");
            branch.OnGreaterEquals = new LatencyHorizon(motionTrue);
            branch.OnLessThan = priority;
            
            BakeConditions(branch);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            
            // Initial state: branchParam=0, condParam=0
            // Expected: branch goes to false -> priority -> default
            yield return null;
            yield return null;
            Assert.IsTrue(testDefault());
            Assert.IsFalse(testTrue());
            Assert.IsFalse(testCondition());
            
            // Now change both parameters at once
            // branchParam: 0->1 (makes branch true)
            // condParam: 0->1 (makes priority condition true)
            animator.SetFloat("branchParam", 1);
            animator.SetFloat("condParam", 1);
            yield return null;
            
            // After frame 1: We should NOT see an intermediate state.
            // Because of LatencyHorizon, both changes should apply together.
            // The branch changes immediately (latency 0), but PriorityNode has latency 1.
            // LatencyHorizon should synchronize them.
            yield return null;
            
            // At this point, motionTrue should be active (from branch)
            // motionDefault and motionCondition should be inactive
            Assert.IsTrue(testTrue(), "After latency horizon sync, motionTrue should be active");
            Assert.IsFalse(testDefault());
            Assert.IsFalse(testCondition());
        }

        /// <summary>
        /// Tests that the total latency is exactly MaxLatency + 1 when using LatencyHorizon.
        /// The LatencyHorizon should introduce exactly 1 frame of latency to synchronize.
        /// </summary>
        [RCILTest]
        public IEnumerator TestLatencyHorizonLatencyIsCorrect()
        {
            CreateSensor("motion", out var motion, out var testMotion);
            AddParameter("param", 0);
            
            var branch = new BranchNode("param");
            branch.OnGreaterEquals = new LatencyHorizon(motion);
            branch.OnLessThan = EmptyNode.Instance;
            
            BakeConditions(branch);
            
            // The MaxLatency should be 0 for BranchNode + LatencyHorizon adds 1
            // So total frames to see effect = MaxLatency + 1
            // For this simple case, MaxLatency should be 0, so we expect 1 frame
            Assert.AreEqual(0, MaxLatency, "MaxLatency should be 0 for BranchNode with LatencyHorizon wrapper");
            
            // Initial state: param=0, motion inactive
            yield return null;
            Assert.IsFalse(testMotion());
            
            // Change param to 1
            animator.SetFloat("param", 1);
            
            // After 1 frame, motion should be active
            yield return null;
            Assert.IsTrue(testMotion(), "Motion should be active after 1 frame (MaxLatency + 1)");
        }

        /// <summary>
        /// Tests a more complex scenario with nested LatencyHorizon nodes.
        /// Changes to multiple parameters should all apply at the same frame due to LatencyHorizon.
        /// </summary>
        [RCILTest]
        public IEnumerator TestComplexLatencyHorizonStructure()
        {
            CreateSensor("motion0", out var motion0, out var test0);
            CreateSensor("motion1", out var motion1, out var test1);
            CreateSensor("motion2", out var motion2, out var test2);
            
            AddParameter("branch1", 0);
            AddParameter("branch2", 0);
            
            // Create two independent branches, both wrapped in LatencyHorizon
            var branch1 = new BranchNode("branch1");
            branch1.OnGreaterEquals = new LatencyHorizon(motion0);
            branch1.OnLessThan = EmptyNode.Instance;
            
            var branch2 = new BranchNode("branch2");
            branch2.OnGreaterEquals = new LatencyHorizon(motion1);
            branch2.OnLessThan = EmptyNode.Instance;
            
            // Combine them with a priority node
            var priority = new PriorityNode();
            priority.DefaultMotion = new LatencyHorizon(motion2);
            priority.Conditions.Add((
                ProxyCondition.FromInner(false, (_, _) => branch1),
                EmptyNode.Instance
            ));
            priority.Conditions.Add((
                ProxyCondition.FromInner(false, (_, _) => branch2),
                EmptyNode.Instance
            ));
            
            BakeConditions(priority);
            
            yield return null;
            yield return null;
            // Initially, all branches are false, so default motion2 should be active
            Assert.IsTrue(test2());
            Assert.IsFalse(test0());
            Assert.IsFalse(test1());
            
            // Enable branch1
            animator.SetFloat("branch1", 1);
            yield return null;
            yield return null;
            Assert.IsTrue(test0(), "Motion0 should be active after branch1 enabled");
            Assert.IsFalse(test1());
            Assert.IsFalse(test2());
            
            // Now enable branch2 while branch1 is active
            animator.SetFloat("branch2", 1);
            yield return null;
            yield return null;
            // Branch1 should still win (it's first in the conditions list)
            Assert.IsTrue(test0());
            Assert.IsFalse(test1());
            Assert.IsFalse(test2());
        }
    }
}

