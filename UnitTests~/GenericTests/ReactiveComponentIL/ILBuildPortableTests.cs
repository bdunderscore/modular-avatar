using NUnit.Framework;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;

namespace UnitTestsReactiveComponentIL
{
    internal sealed class ILBuildPortableTests
    {
        [Test]
        public void SplitIntoSubgraphs_RetainsSharedParameters()
        {
            var graph = new ReactionGraph();
            graph.Parameters.EnsureParameter("P", 1f);
            var parameters = graph.Parameters;
            graph.AddNode(new ReactionNode(
                new ParameterExpression("P", 0.5f, ParameterExpression.ConditionMode.GreaterThan),
                new DriveInternalParameter("result", true)
            ));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("result"),
                new DriveParameter("output", 1f)
            ));
            var graphs = SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(1, graphs.Count);
            Assert.AreSame(parameters, graphs[0].Parameters);
            Assert.AreEqual(1f, graphs[0].Parameters.GetParameterInitialValue("P"));
        }
    }
}
