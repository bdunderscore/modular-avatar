using System;
using System.Collections;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnitTests.SharedInterfacesImpl;

namespace UnitTestsReactiveComponentIL
{
    public class OneDBlendTreeTest : ReactiveComponentILTestBase
    {
        [RCILTest]
        public IEnumerator ThresholdsSelectStatesWithoutBlending()
        {
            CreateSensor("low", out var lowMotion, out var lowActive);
            CreateSensor("middle", out var middleMotion, out var middleActive);
            CreateSensor("high", out var highMotion, out var highActive);
            AddParameter("test", -1f);

            var blend = new OneDBlendNode("test");
            blend.Nodes.Add((float.NegativeInfinity, lowMotion));
            blend.Nodes.Add((0f, middleMotion));
            blend.Nodes.Add((1f, highMotion));

            BakeConditions(blend);
            Assert.IsNotNull(animator.runtimeAnimatorController);

            var sensors = new[] { lowActive, middleActive, highActive };
            var cases = new[]
            {
                (-1f, 0),
                (0f.NextSmallest(), 0),
                (0f, 1),
                (0f.NextLargest(), 1),
                (0.5f, 1),
                (1f.NextSmallest(), 1),
                (1f, 2),
                (1f.NextLargest(), 2),
            };

            foreach (var (value, expectedState) in cases)
            {
                animator.SetFloat("test", value);
                yield return null;
                AssertOnlyStateActive(sensors, expectedState, value);
            }
        }

        [RCILTest]
        public IEnumerator CoalescedBranchesPreserveExactThresholdsWithoutBlending()
        {
            CreateSensor("low", out var lowMotion, out var lowActive);
            CreateSensor("middle", out var middleMotion, out var middleActive);
            CreateSensor("high", out var highMotion, out var highActive);
            AddParameter("test", -1f);

            IMotionNode root = new BranchNode(
                "test",
                lowMotion,
                new BranchNode("test", middleMotion, highMotion) { Threshold = 1f }
            ) { Threshold = 0f };

            CoalesceBranchesTransform.Apply(ref root);

            var blend = root as OneDBlendNode;
            Assert.That(blend, Is.Not.Null, "The test must exercise the coalesced OneDBlendNode path");
            Assert.That(
                blend.Nodes.Select(node => node.Item1),
                Is.EqualTo(new[] { float.NegativeInfinity, 0f.NextLargest(), 1f.NextLargest() })
            );

            BakeConditions(root);
            Assert.IsNotNull(animator.runtimeAnimatorController);

            var sensors = new[] { lowActive, middleActive, highActive };
            var cases = new[]
            {
                (-1f, 0),
                (0f, 0),
                (0f.NextLargest(), 1),
                (0.5f, 1),
                (1f, 1),
                (1f.NextLargest(), 2),
            };

            foreach (var (value, expectedState) in cases)
            {
                animator.SetFloat("test", value);
                yield return null;
                AssertOnlyStateActive(sensors, expectedState, value);
            }
        }

        private static void AssertOnlyStateActive(Func<bool>[] sensors, int expectedState, float parameterValue)
        {
            for (var i = 0; i < sensors.Length; i++)
            {
                Assert.That(
                    sensors[i](),
                    Is.EqualTo(i == expectedState),
                    $"Parameter value {parameterValue:R} selected or blended state {i}; expected only state {expectedState}"
                );
            }
        }
    }
}
