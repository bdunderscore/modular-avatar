using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using NUnit.Framework;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class ConvertToInternalParametersTests : TestBase
    {
        private GameObject _root;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
        }
        private void ConvertAndSimplify(ReactionGraph graph)
        {
            ConvertToInternalParametersTransform.Apply(new TestReactionBackend(graph.Parameters), graph);
            BooleanSimplifyTransform.Apply(graph);
        }


        [Test]
        public void ConvertObjectActiveStateToInternalParameter()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            // Check that the expression was converted to InternalParameterCondition
            Assert.IsInstanceOf<InternalParameterCondition>(graph.Nodes[0].Expression);
            var ipc = (InternalParameterCondition)graph.Nodes[0].Expression;

            // Check that the parameter was created in the context
            Assert.IsTrue(ipc.ParameterName.StartsWith("$$MA/RC/ObjActive/obj$"));
            // Check that the parameter was added to the graph.
            Assert.IsTrue(graph.Parameters.ParameterDefaults.ContainsKey(ipc.ParameterName));
            Assert.AreEqual(obj.activeSelf ? 1 : 0, graph.Parameters.ParameterDefaults[ipc.ParameterName]);
        }

        [Test]
        public void ConvertDriveActiveStateToInternalParameter()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveActiveState(obj, true)
            ));

            ConvertAndSimplify(graph);

            // The transform keeps the original DriveActiveState and appends a DriveInternalParameter
            Assert.AreEqual(1, graph.Nodes[0].Effects.OfType<DriveActiveState>().Count());
            Assert.AreEqual(2, graph.Nodes[0].Effects.OfType<DriveInternalParameter>().Count());
            var dip = graph.Nodes[0].Effects.OfType<DriveInternalParameter>()
                .Single(effect => effect.ParameterName.StartsWith("$$MA/RC/ObjActive/obj$"));

            // Check that it drives the same parameter
            Assert.AreEqual(true, dip.State);
        }

        [Test]
        public void ConvertMultipleObjectsToSeparateParameters()
        {
            var obj1 = CreateChild(_root, "obj1");
            var obj2 = CreateChild(_root, "obj2");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj1, ObjectActiveState.State.Active),
                new NullAction(new object())
            ));
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj2, ObjectActiveState.State.Active),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            // Check that two different parameters were created
            var ipc1 = (InternalParameterCondition)graph.Nodes[0].Expression;
            var ipc2 = (InternalParameterCondition)graph.Nodes[1].Expression;

            Assert.AreNotEqual(ipc1.ParameterName, ipc2.ParameterName);
            Assert.IsTrue(ipc1.ParameterName.Contains("obj1"));
            Assert.IsTrue(ipc2.ParameterName.Contains("obj2"));
        }

        [Test]
        public void ConvertSameObjectToSameParameter()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new NullAction(new object())
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveActiveState(obj, true)
            ));

            ConvertAndSimplify(graph);

            // Check that both use the same parameter
            var ipc = (InternalParameterCondition)graph.Nodes[0].Expression;
            var dip = graph.Nodes[1].Effects.OfType<DriveInternalParameter>()
                .Single(effect => effect.ParameterName.StartsWith("$$MA/RC/ObjActive/obj$"));

            Assert.AreEqual(ipc.ParameterName, dip.ParameterName);
        }

        [Test]
        public void ConvertNestedObjectActiveStates()
        {
            var obj1 = CreateChild(_root, "obj1");
            var obj2 = CreateChild(_root, "obj2");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new AndNode(
                    new ObjectActiveState(obj1, ObjectActiveState.State.Active),
                    new ObjectActiveState(obj2, ObjectActiveState.State.Active)
                ),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            // Check that the nested structure was preserved
            Assert.IsInstanceOf<AndNode>(graph.Nodes[0].Expression);
            var andNode = (AndNode)graph.Nodes[0].Expression;

            Assert.AreEqual(2, andNode.Children.Count);
            Assert.IsInstanceOf<InternalParameterCondition>(andNode.Children[0]);
            Assert.IsInstanceOf<InternalParameterCondition>(andNode.Children[1]);
        }

        [Test]
        public void ConvertComplexExpression()
        {
            var obj1 = CreateChild(_root, "obj1");
            var obj2 = CreateChild(_root, "obj2");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new OrNode(
                    new AndNode(
                        new ObjectActiveState(obj1, ObjectActiveState.State.Active),
                        new ParameterExpression("param1")
                    ),
                    new NotNode(
                        new ObjectActiveState(obj2, ObjectActiveState.State.Inactive)
                    )
                ),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            // Check the structure: OrNode -> [AndNode, NotNode]
            Assert.IsInstanceOf<OrNode>(graph.Nodes[0].Expression);
            var orNode = (OrNode)graph.Nodes[0].Expression;
            Assert.AreEqual(2, orNode.Children.Count);

            // Check AndNode children
            Assert.IsInstanceOf<AndNode>(orNode.Children[0]);
            var andNode = (AndNode)orNode.Children[0];
            Assert.AreEqual(2, andNode.Children.Count);
            Assert.IsInstanceOf<InternalParameterCondition>(andNode.Children[0]);
            Assert.IsInstanceOf<ParameterExpression>(andNode.Children[1]);

            // The inactive-state replacement is negated by the original expression, so simplify
            // collapses the resulting double negation to the active parameter condition.
            Assert.IsInstanceOf<InternalParameterCondition>(orNode.Children[1]);
        }

        [Test]
        public void PreservesNonObjectActiveExpressions()
        {
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new ParameterExpression("param1"),
                new NullAction(new object())
            ));
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new DriveParameter("param2", 1.0f)
            ));

            ConvertAndSimplify(graph);

            // Check that non-ObjectActive expressions and actions are preserved
            Assert.IsInstanceOf<ParameterExpression>(graph.Nodes[0].Expression);
            Assert.AreEqual(1, graph.Nodes[1].Effects.OfType<DriveParameter>().Count());
        }

        [Test]
        public void HandlesInactiveObjects()
        {
            var obj = CreateChild(_root, "obj");
            obj.SetActive(false);
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            // Check that the parameter default value matches the object's initial state.
            var ipc = (InternalParameterCondition)graph.Nodes[0].Expression;
            Assert.AreEqual(0, graph.Parameters.ParameterDefaults[ipc.ParameterName]);
        }

        [Test]
        public void ConvertMultipleEffectsOnSameNode()
        {
            var obj1 = CreateChild(_root, "obj1");
            var obj2 = CreateChild(_root, "obj2");
            var graph = new ReactionGraph();

            var node = new ReactionNode(
                new Constant(true),
                new DriveActiveState(obj1, true)
            );
            node.Effects.Add(new DriveActiveState(obj2, false));
            graph.AddNode(node);

            ConvertAndSimplify(graph);

            // Original DriveActiveState effects are kept; DriveInternalParameter effects are appended
            Assert.AreEqual(2, graph.Nodes[0].Effects.OfType<DriveActiveState>().Count());
            Assert.AreEqual(4, graph.Nodes[0].Effects.OfType<DriveInternalParameter>().Count());

            var dip1 = graph.Nodes[0].Effects.OfType<DriveInternalParameter>()
                .Single(d => d.ParameterName.Contains("ObjActive/obj1"));
            var dip2 = graph.Nodes[0].Effects.OfType<DriveInternalParameter>()
                .Single(d => d.ParameterName.Contains("ObjActive/obj2"));

            Assert.AreEqual(true, dip1.State);
            Assert.AreEqual(false, dip2.State);
        }

        [Test]
        public void ConvertDifferentStateModes()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();

            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new NullAction(new object())
            ));
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Inactive),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            // Both should reference the same parameter
            var ipc1 = (InternalParameterCondition)graph.Nodes[0].Expression;
            var notIpc2 = (NotNode)graph.Nodes[1].Expression;
            var ipc2 = (InternalParameterCondition)notIpc2.Inner;

            Assert.AreEqual(ipc1.ParameterName, ipc2.ParameterName);
        }

        [Test]
        public void NotDrivenWithoutDrivers_ConvertsToTrue()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.NotDriven),
                new NullAction(new object())
            ));

            ConvertAndSimplify(graph);

            Assert.AreEqual(new Constant(true), graph.Nodes[0].Expression);
            Assert.IsFalse(graph.Parameters.ParameterDefaults.Keys.Any(name => name.Contains("ObjDriven/obj")));
        }

        [Test]
        public void NotDrivenWithDriver_TracksDriverAndAddsReset()
        {
            var obj = CreateChild(_root, "obj");
            var driverCondition = new ParameterExpression("driver");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.NotDriven),
                new NullAction(new object())
            ));
            graph.AddNode(new ReactionNode(driverCondition, new DriveActiveState(obj, true)));

            ConvertAndSimplify(graph);

            var notDriven = (NotNode)graph.Nodes[0].Expression;
            var drivenParameter = ((InternalParameterCondition)notDriven.Inner).ParameterName;
            Assert.IsTrue(drivenParameter.Contains("ObjDriven/obj"));
            var driven = graph.Nodes[1].Effects.OfType<DriveInternalParameter>()
                .Single(effect => effect.ParameterName == drivenParameter);
            Assert.IsTrue(driven.State);

            var resetNode = graph.Nodes.Last();
            Assert.AreEqual(new NotNode(driverCondition), resetNode.Expression);
            var reset = (DriveInternalParameter)resetNode.Effects.Single();
            Assert.AreEqual(drivenParameter, reset.ParameterName);
            Assert.IsFalse(reset.State);
        }

        [Test]
        public void NotDrivenWithMultipleDrivers_ResetWhenNeitherDriverApplies()
        {
            var obj = CreateChild(_root, "obj");
            var firstDriver = new ParameterExpression("first");
            var secondDriver = new ParameterExpression("second");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.NotDriven),
                new NullAction(new object())
            ));
            graph.AddNode(new ReactionNode(firstDriver, new DriveActiveState(obj, true)));
            graph.AddNode(new ReactionNode(secondDriver, new DriveActiveState(obj, false)));

            ConvertAndSimplify(graph);

            var resetNode = graph.Nodes.Last();
            Assert.AreEqual(new NotNode(new OrNode(firstDriver, secondDriver)), resetNode.Expression);
            var reset = (DriveInternalParameter)resetNode.Effects.Single();
            Assert.IsTrue(reset.ParameterName.Contains("ObjDriven/obj"));
            Assert.IsFalse(reset.State);
        }

        [Test]
        public void ConvertToInternal_CreatesMultiEffectNodes_DecomposeProducesValidSingleEffectNodes()
        {
            // ConvertToInternalParametersTransform appends DriveInternalParameter to nodes that have
            // DriveActiveState effects, producing multi-effect nodes. DecomposeTransform (step 7 in
            // the pipeline) must then split those back to single-effect nodes with contiguous priorities.
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveActiveState(obj, true)));

            ConvertAndSimplify(graph);

            Assert.AreEqual(2, graph.Nodes.Count, "ConvertToInternal adds the not-driven reset node");
            Assert.AreEqual(3, graph.Nodes[0].Effects.Count,
                "Node should carry active-state and driven-state internal parameter effects");

            DecomposeTransform.Apply(graph);

            Assert.AreEqual(4, graph.Nodes.Count, "One node per effect after decomposition");
            Assert.IsTrue(graph.Nodes.All(n => n.Effects.Count == 1), "All nodes must be single-effect");
            Assert.AreEqual(1, graph.Nodes.Count(n => n.Effects[0] is DriveActiveState));
            Assert.AreEqual(3, graph.Nodes.Count(n => n.Effects[0] is DriveInternalParameter));
            CollectionAssert.AreEqual(
                Enumerable.Range(0, graph.Nodes.Count).ToList(),
                graph.Nodes.Select(node => node.Priority).ToList());
        }
    }
}

