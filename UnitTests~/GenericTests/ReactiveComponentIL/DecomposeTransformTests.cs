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
            var node = new ReactionNode(expr, new DriveInternalParameter("p1", true));
            node.Effects.Add(new DriveInternalParameter("p2", false));
            node.Effects.Add(new DriveInternalParameter("p3", true));
            graph.AddNode(node);

            DecomposeTransform.Apply(graph);

            Assert.AreEqual(3, graph.Nodes.Count);
            Assert.IsTrue(graph.Nodes.All(n => n.Effects.Count == 1));
            // All split nodes share the same expression
            Assert.IsTrue(graph.Nodes.All(n => n.Expression == expr));
            // Each effect appears exactly once
            var paramNames = graph.Nodes.Select(n => ((DriveInternalParameter)n.Effects[0]).ParameterName).ToList();
            CollectionAssert.AreEquivalent(new[] { "p1", "p2", "p3" }, paramNames);
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

            var nodeA = new ReactionNode(new Constant(true), new DriveInternalParameter("a1", true));
            nodeA.Effects.Add(new DriveInternalParameter("a2", false));
            graph.AddNode(nodeA);

            graph.AddNode(new ReactionNode(new Constant(false), new DriveInternalParameter("b1", true)));

            var nodeC = new ReactionNode(new ParameterExpression("x"), new DriveInternalParameter("c1", true));
            nodeC.Effects.Add(new DriveInternalParameter("c2", true));
            nodeC.Effects.Add(new DriveInternalParameter("c3", false));
            graph.AddNode(nodeC);

            DecomposeTransform.Apply(graph);

            // 2 + 1 + 3 = 6 nodes total
            Assert.AreEqual(6, graph.Nodes.Count);
            Assert.IsTrue(graph.Nodes.All(n => n.Effects.Count == 1));

            // Priorities are 0..5 contiguously
            var priorities = graph.Nodes.Select(n => n.Priority).OrderBy(p => p).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(0, 6).ToList(), priorities);
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
            // After the first decompose all nodes are single-effect; a second pass must be a no-op.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p1", true)));
            graph.AddNode(new ReactionNode(new Constant(false), new DriveInternalParameter("p2", false)));

            DecomposeTransform.Apply(graph);
            var countAfterFirst = graph.Nodes.Count;
            DecomposeTransform.Apply(graph);

            Assert.AreEqual(countAfterFirst, graph.Nodes.Count);
        }
    }
}
