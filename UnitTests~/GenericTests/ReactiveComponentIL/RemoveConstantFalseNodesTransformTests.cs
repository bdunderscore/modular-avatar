using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class RemoveConstantFalseNodesTransformTests : TestBase
    {
        [Test]
        public void Apply_RemovesFalseNodesWhilePreservingRemainingNodesAndParameters()
        {
            var graph = new ReactionGraph();
            graph.Parameters.EnsureParameter("condition", 1f);
            var parameters = graph.Parameters;

            var trueEffect = new DriveInternalParameter("true", true);
            var additionalTrueEffect = new DriveInternalParameter("also-true", false);
            var trueNode = new ReactionNode(new Constant(true), trueEffect);
            trueNode.Effects.Add(additionalTrueEffect);
            var falseNode = new ReactionNode(new Constant(false), new DriveInternalParameter("false", false));
            falseNode.Effects.Add(new DriveInternalParameter("also-false", true));
            var conditionalEffect = new DriveInternalParameter("conditional", true);
            var conditionalNode = new ReactionNode(new ParameterExpression("condition"), conditionalEffect);
            graph.AddNode(trueNode);
            graph.AddNode(falseNode);
            graph.AddNode(conditionalNode);

            RemoveConstantFalseNodesTransform.Apply(graph);

            CollectionAssert.AreEqual(new[] { trueNode, conditionalNode }, graph.Nodes);
            CollectionAssert.AreEqual(new IAction[] { trueEffect, additionalTrueEffect }, graph.Nodes[0].Effects);
            Assert.AreSame(conditionalEffect, graph.Nodes[1].Effects[0]);
            Assert.AreSame(parameters, graph.Parameters);
            Assert.AreEqual(1f, graph.Parameters.GetParameterInitialValue("condition"));
        }
    }
}
