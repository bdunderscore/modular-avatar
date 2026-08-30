using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class DecomposeTransformTests : TestBase
    {
        [Test]
        public void EmptyGraph_NoError()
        {
            var graph = new ReactionGraph();
            DecomposeTransform.Apply(graph);
            Assert.AreEqual(0, graph.Nodes.Count);
        }

        [Test]
        public void SingleNodeSingleEffect_NoStructuralChange()
        {
            var graph = new ReactionGraph();
            var effect = new NullAction();
            graph.AddNode(new ReactionNode(new Constant(true), effect));

            DecomposeTransform.Apply(graph);

            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.AreEqual(1, graph.Nodes[0].Effects.Count);
            Assert.AreSame(effect, graph.Nodes[0].Effects[0]);
        }

        [Test]
        public void SingleNodeMultipleEffects_SplitsIntoOneNodePerEffect()
        {
            var graph = new ReactionGraph();
            var expr = new Constant(true);
            var effect1 = new DriveInternalParameter("p1", true);
            var effect2 = new DriveInternalParameter("p2", false);
            var effect3 = new DriveInternalParameter("p3", true);
            var node = new ReactionNode(expr, effect1);
            node.Effects.Add(effect2);
            node.Effects.Add(effect3);
            graph.AddNode(node);

            DecomposeTransform.Apply(graph);

            Assert.AreEqual(3, graph.Nodes.Count);
            Assert.IsTrue(graph.Nodes.All(n => n.Effects.Count == 1));
            Assert.IsTrue(graph.Nodes.All(n => n.Expression == expr));
            CollectionAssert.AreEquivalent(
                new[] { effect1, effect2, effect3 },
                graph.Nodes.Select(n => n.Effects.Single()).ToList());
        }

        [Test]
        public void MultipleNodesAllSingleEffect_PrioritiesRenumbered()
        {
            var graph = new ReactionGraph();
            for (var i = 0; i < 3; i++)
                graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p" + i, true)));

            DecomposeTransform.Apply(graph);

            Assert.AreEqual(3, graph.Nodes.Count);
            for (var i = 0; i < 3; i++)
                Assert.AreEqual(i, graph.Nodes[i].Priority);
        }

        [Test]
        public void MultipleNodesWithMultipleEffects_AllSplitAndPrioritiesContiguous()
        {
            var graph = new ReactionGraph();

            var expressionA = new Constant(true);
            var effectA1 = new DriveInternalParameter("a1", true);
            var effectA2 = new DriveInternalParameter("a2", false);
            var nodeA = new ReactionNode(expressionA, effectA1);
            nodeA.Effects.Add(effectA2);
            graph.AddNode(nodeA);

            var expressionB = new Constant(false);
            var effectB1 = new DriveInternalParameter("b1", true);
            graph.AddNode(new ReactionNode(expressionB, effectB1));

            var expressionC = new ParameterExpression("x");
            var effectC1 = new DriveInternalParameter("c1", true);
            var effectC2 = new DriveInternalParameter("c2", true);
            var effectC3 = new DriveInternalParameter("c3", false);
            var nodeC = new ReactionNode(expressionC, effectC1);
            nodeC.Effects.Add(effectC2);
            nodeC.Effects.Add(effectC3);
            graph.AddNode(nodeC);

            DecomposeTransform.Apply(graph);

            Assert.AreEqual(6, graph.Nodes.Count);
            Assert.IsTrue(graph.Nodes.All(n => n.Effects.Count == 1));

            var expectedPairs = new (IAction effect, IExpression expression)[]
            {
                (effectA1, expressionA),
                (effectA2, expressionA),
                (effectB1, expressionB),
                (effectC1, expressionC),
                (effectC2, expressionC),
                (effectC3, expressionC),
            };
            foreach (var (effect, expression) in expectedPairs)
            {
                var splitNode = graph.Nodes.Single(n => ReferenceEquals(n.Effects.Single(), effect));
                Assert.AreSame(expression, splitNode.Expression);
            }

            CollectionAssert.AreEqual(
                Enumerable.Range(0, 6).ToList(),
                graph.Nodes.Select(n => n.Priority).ToList());
        }

        [Test]
        public void SplitNodesPreserveOriginalExpression()
        {
            var graph = new ReactionGraph();
            var expr = new ParameterExpression("cond");
            var node = new ReactionNode(expr, new DriveInternalParameter("p1", true));
            node.Effects.Add(new DriveInternalParameter("p2", true));
            graph.AddNode(node);

            DecomposeTransform.Apply(graph);

            Assert.IsTrue(graph.Nodes.All(n => n.Expression == expr),
                "All split nodes must reference the same expression object");
        }

        [Test]
        public void AppliedTwice_IdempotentOnSingleEffectGraph()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p1", true)));
            graph.AddNode(new ReactionNode(new Constant(false), new DriveInternalParameter("p2", false)));

            DecomposeTransform.Apply(graph);
            var effectsAfterFirst = graph.Nodes.Select(node => node.Effects.Single()).ToList();
            var expressionsAfterFirst = graph.Nodes.Select(node => node.Expression).ToList();
            var prioritiesAfterFirst = graph.Nodes.Select(node => node.Priority).ToList();

            DecomposeTransform.Apply(graph);

            CollectionAssert.AreEqual(
                effectsAfterFirst,
                graph.Nodes.Select(node => node.Effects.Single()).ToList());
            CollectionAssert.AreEqual(
                expressionsAfterFirst,
                graph.Nodes.Select(node => node.Expression).ToList());
            CollectionAssert.AreEqual(
                prioritiesAfterFirst,
                graph.Nodes.Select(node => node.Priority).ToList());
        }
    }
}
