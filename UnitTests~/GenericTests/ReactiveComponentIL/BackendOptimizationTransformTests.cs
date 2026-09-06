using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class BackendOptimizationTransformTests : TestBase
    {
        private UnityBlendTreeBackend _backend;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            var context = CreateContext(CreateRoot("root"));
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controller = VirtualAnimatorController.Create(animatorServices.ControllerContext.CloneContext);
            _backend = new UnityBlendTreeBackend(context, controller);
        }

        [Test]
        public void MergeEffectNodes_EquivalentConditions_EmitsAllMergedActions()
        {
            var condition = new InternalParameterCondition("enabled");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(condition, new DriveParameter("first", 1f)));
            graph.AddNode(new ReactionNode(condition, new DriveParameter("second", 2f)));

            var effectGroups = AlignNodesTransform.CreateEffectGroups(_backend, graph);
            var merged = MergeEffectNodesTransform.MergeNodes(_backend, effectGroups.Values.ToList());

            Assert.That(merged, Has.Count.EqualTo(1));
            Assert.That(merged[0].Nodes, Has.Count.EqualTo(1));
            Assert.That(merged[0].Nodes[0].Effects, Has.Count.EqualTo(2));

            IMotionNode emitted = merged[0].Emit();
            while (emitted is ProxyNode proxy) emitted = proxy.Target;
            var branch = emitted as BranchNode;
            Assert.That(branch, Is.Not.Null);
            var motion = branch.OnGreaterThan as MotionNode;
            Assert.That(motion, Is.Not.Null);
            var clip = motion.Motion as VirtualClip;
            Assert.That(clip, Is.Not.Null);
            var firstCurve = clip.GetFloatCurve("", typeof(Animator), "first");
            var secondCurve = clip.GetFloatCurve("", typeof(Animator), "second");
            Assert.That(firstCurve, Is.Not.Null);
            Assert.That(secondCurve, Is.Not.Null);
            Assert.That(firstCurve.Evaluate(0), Is.EqualTo(1f));
            Assert.That(secondCurve.Evaluate(0), Is.EqualTo(2f));
        }

        [Test]
        public void MergeEffectNodes_DifferentConditions_RemainsSeparate()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new InternalParameterCondition("first"), new DriveParameter("first", 1f)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("second"), new DriveParameter("second", 2f)));

            var effectGroups = AlignNodesTransform.CreateEffectGroups(_backend, graph);
            var merged = MergeEffectNodesTransform.MergeNodes(_backend, effectGroups.Values.ToList());

            Assert.That(merged, Has.Count.EqualTo(2));
        }

        [Test]
        public void MergeEffectNodes_InitiallyActive_AppliesEveryTargetBaseState()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveParameter("first", 1f)));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveParameter("second", 2f)));
            _backend.PreprocessGraph(graph);

            var effectGroups = AlignNodesTransform.CreateEffectGroups(_backend, graph);
            var merged = MergeEffectNodesTransform.MergeNodes(_backend, effectGroups.Values.ToList());
            AssignInitialGroupStatesTransform.Apply(_backend, merged);

            Assert.That(merged[0].DefaultNode, Is.EqualTo(0));
            Assert.That(_backend.GetParameterInitialValue("first"), Is.EqualTo(1f));
            Assert.That(_backend.GetParameterInitialValue("second"), Is.EqualTo(2f));
        }

        [Test]
        public void CoalesceBranches_AdjacentEqualRanges_MergesWithoutLosingRange()
        {
            var repeated = new EmptyNode();
            var final = new EmptyNode();
            IMotionNode root = new BranchNode("parameter", repeated,
                new BranchNode("parameter", repeated, final) { Threshold = 1f }) { Threshold = 0f };

            CoalesceBranchesTransform.Apply(ref root);

            var blend = root as OneDBlendNode;
            Assert.That(blend, Is.Not.Null);
            Assert.That(blend.Nodes, Has.Count.EqualTo(2));
            Assert.That(blend.Nodes[0].Item1, Is.EqualTo(float.NegativeInfinity));
            Assert.That(blend.Nodes[0].Item2, Is.SameAs(repeated));
            Assert.That(blend.Nodes[1].Item1, Is.EqualTo(1f.NextLargest()));
            Assert.That(blend.Nodes[1].Item2, Is.SameAs(final));
        }

        [Test]
        public void CoalesceBranches_DifferentParameters_PreservesNestedCondition()
        {
            var onFalse = new EmptyNode();
            var onTrue = new EmptyNode();
            IMotionNode root = new BranchNode("outer", new EmptyNode(),
                new BranchNode("inner", onFalse, onTrue) { Threshold = 1f }) { Threshold = 0f };

            CoalesceBranchesTransform.Apply(ref root);

            var outer = root as OneDBlendNode;
            Assert.That(outer, Is.Not.Null);
            Assert.That(outer.Parameter, Is.EqualTo("outer"));
            var inner = outer.Nodes.Single(node => node.Item2 is OneDBlendNode).Item2 as OneDBlendNode;
            Assert.That(inner.Parameter, Is.EqualTo("inner"));
            Assert.That(inner.Nodes.Select(node => node.Item2), Is.EquivalentTo(new[] { onFalse, onTrue }));
        }
    }
}
