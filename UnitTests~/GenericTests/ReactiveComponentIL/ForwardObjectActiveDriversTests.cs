using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class ForwardObjectActiveDriversTests : TestBase
    {
        [Test]
        public void SingleDriverMatching_ReplacesWithDriverExpr()
        {
            var obj = CreateRoot("obj");
            var driverExpr = new ParameterExpression("p1");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction(new object())));
            graph.AddNode(new ReactionNode(driverExpr, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(driverExpr, graph.Nodes[0].Expression);
        }

        [Test]
        public void SingleDriverOpposite_ReplacesWithFalse()
        {
            var obj = CreateRoot("obj");
            var driverExpr = new ParameterExpression("p1");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction(new object())));
            graph.AddNode(new ReactionNode(driverExpr, new DriveActiveState(obj, false)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new Constant(false), graph.Nodes[0].Expression);
        }

        [Test]
        public void TwoDriversBothMatch_Or()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var e2 = new ParameterExpression("p2");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));
            graph.AddNode(new ReactionNode(e2, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new OrNode(e1, e2), graph.Nodes[0].Expression);
        }

        [Test]
        public void TwoDriversFirstMatchSecondOpposite_AndNot()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var e2 = new ParameterExpression("p2");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));
            graph.AddNode(new ReactionNode(e2, new DriveActiveState(obj, false)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new AndNode(e1, new NotNode(e2)), graph.Nodes[0].Expression);
        }

        [Test]
        public void TwoDriversFirstOppositeSecondMatch_PicksSecond()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var e2 = new ParameterExpression("p2");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, false)));
            graph.AddNode(new ReactionNode(e2, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(e2, graph.Nodes[0].Expression);
        }

        [Test]
        public void AllDriversOpposite_MoreThanTwo_CollapsesFalse()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var e2 = new ParameterExpression("p2");
            var e3 = new ParameterExpression("p3");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, false)));
            graph.AddNode(new ReactionNode(e2, new DriveActiveState(obj, false)));
            graph.AddNode(new ReactionNode(e3, new DriveActiveState(obj, false)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new Constant(false), graph.Nodes[0].Expression);
        }

        [Test]
        public void NotDriven_SingleDriver_Not()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.NotDriven), new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new NotNode(e1), graph.Nodes[0].Expression);
        }

        [Test]
        public void NotDriven_TwoDrivers_NotOr()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var e2 = new ParameterExpression("p2");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.NotDriven), new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));
            graph.AddNode(new ReactionNode(e2, new DriveActiveState(obj, false)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new NotNode(new OrNode(e1, e2)), graph.Nodes[0].Expression);
        }

        [Test]
        public void NotDriven_ThreeDrivers_NoForwarding()
        {
            var obj = CreateRoot("obj");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.NotDriven), new NullAction(new object())));
            graph.AddNode(new ReactionNode(new ParameterExpression("p1"), new DriveActiveState(obj, true)));
            graph.AddNode(new ReactionNode(new ParameterExpression("p2"), new DriveActiveState(obj, false)));
            graph.AddNode(new ReactionNode(new ParameterExpression("p3"), new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            Assert.AreEqual(new ObjectActiveState(obj, ObjectActiveState.State.NotDriven), graph.Nodes[0].Expression);
        }

        [Test]
        public void MultipleObjectActiveStatesSameObject_Forwards()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var expr = new AndNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new ObjectActiveState(obj, ObjectActiveState.State.Active));
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(expr, new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            // Both OAS nodes should be replaced with e1
            var expected = new AndNode(e1, e1);
            Assert.AreEqual(expected, graph.Nodes[0].Expression);
        }

        [Test]
        public void MultipleObjectActiveStatesDifferentObjects_NoForwarding()
        {
            var obj1 = CreateRoot("obj1");
            var obj2 = CreateRoot("obj2");
            var expr = new AndNode(new ObjectActiveState(obj1, ObjectActiveState.State.Active), new ObjectActiveState(obj2, ObjectActiveState.State.Active));
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(expr, new NullAction(new object())));
            graph.AddNode(new ReactionNode(new ParameterExpression("p1"), new DriveActiveState(obj1, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            // Should not forward since expressions reference different objects
            Assert.AreEqual(expr, graph.Nodes[0].Expression);
        }

        [Test]
        public void MixedStateModes_ProcessedSeparately()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var expr = new AndNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new ObjectActiveState(obj, ObjectActiveState.State.Inactive)
            );
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(expr, new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            // Active OAS is driven to true -> replaced with e1
            // Inactive OAS is driven to true (opposite) -> replaced with false
            var expected = new AndNode(e1, new Constant(false));
            Assert.AreEqual(expected, graph.Nodes[0].Expression);
        }

        [Test]
        public void MixedStateModesWithNotDriven()
        {
            var obj = CreateRoot("obj");
            var e1 = new ParameterExpression("p1");
            var expr = new OrNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new ObjectActiveState(obj, ObjectActiveState.State.NotDriven)
            );
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(expr, new NullAction(new object())));
            graph.AddNode(new ReactionNode(e1, new DriveActiveState(obj, true)));

            ForwardObjectActiveDriversTransform.Apply(graph);

            // Active OAS is driven to true -> replaced with e1
            // NotDriven OAS -> replaced with NOT(e1)
            var expected = new OrNode(e1, new NotNode(e1));
            Assert.AreEqual(expected, graph.Nodes[0].Expression);
        }
    }
}


