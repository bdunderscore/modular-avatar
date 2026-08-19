using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class ReactionGraphTests : TestBase
    {
        private BakeContext _bakeContext;
        private GameObject _root;
        private AnimatorServicesContext _asc;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
            var bc = CreateContext(_root);
            _asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var vac = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, vac);
        }

        [Test]
        public void CheckTransformResults(
            [Values(false, true)] bool hasExternalAnimation,
            [Values(false, true)] bool hasDriver,
            [Values(false, true)] bool wantState
        )
        {
            var obj = CreateChild(_root, "obj");

            var initialExpr = new ObjectActiveState(obj,
                wantState ? ObjectActiveState.State.Active : ObjectActiveState.State.Inactive);
            var node = new ReactionNode(
                initialExpr,
                new NullAction()
            );

            var graph = new ReactionGraph();
            graph.AddNode(node);

            VirtualClip? clip = null;
            if (hasExternalAnimation)
            {
                var controller = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
                var layer = controller.AddLayer(LayerPriority.Default, "layer");
                var state = layer.StateMachine!.AddState("state");
                layer.StateMachine.DefaultState = state;
                clip = VirtualClip.Create("clip");
                clip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve(
                        _asc.ObjectPathRemapper.GetVirtualPathForObject(obj),
                        typeof(GameObject),
                        "m_IsActive"
                    ),
                    AnimationCurve.Constant(0, 1, 0)
                );
                state.Motion = clip;

                _asc.ControllerContext.Controllers["xyzzy"] = controller;
            }

            if (hasDriver)
            {
                graph.AddNode(new ReactionNode(
                    new Constant(false),
                    new DriveActiveState(obj, true)
                ));
            }

            ProcessExternalObjectStateInputsTransform.Apply(graph, _bakeContext);

            var targetRule = graph.Nodes[0].Expression;
            if (targetRule is not OrNode orNode)
                throw new Exception("should be an OrNode at the root: " + targetRule);
            if (orNode.Children[0] is not AndNode andNode)
                throw new Exception("should be an AndNode at the first child: " + targetRule);

            Assert.AreEqual(2, orNode.Children.Count, "wrong number of children at OrNode: " + targetRule);
            Assert.AreEqual(2, andNode.Children.Count, "wrong number of children at AndNode: " + targetRule);

            if (hasExternalAnimation)
            {
                if (andNode.Children[0] is not ParameterExpression pe)
                {
                    throw new Exception("should be a ParameterExpression at the first child: " + targetRule);
                }

                Assert.AreEqual(
                    wantState
                        ? ParameterExpression.ConditionMode.GreaterThan
                        : ParameterExpression.ConditionMode.LessThan,
                    pe.Mode
                );

                Assert.IsNotNull(
                    clip.GetFloatCurve(
                        EditorCurveBinding.FloatCurve(
                            "",
                            typeof(Animator),
                            pe.ParameterName
                        )
                    )
                );

                // Base layer should seed initial active state and parameter defaults
                var path = _asc.ObjectPathRemapper.GetVirtualPathForObject(obj);
                var baseActiveCurve = _bakeContext.BaseLayerClip.GetFloatCurve(
                    EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"));
                Assert.IsNotNull(baseActiveCurve, "Base layer missing active-state default curve");
                Assert.AreEqual(obj.activeSelf ? 1 : 0, baseActiveCurve.keys[0].value);

                var baseParamCurve = _bakeContext.BaseLayerClip.GetFloatCurve(
                    EditorCurveBinding.FloatCurve("", typeof(Animator), pe.ParameterName));
                Assert.IsNotNull(baseParamCurve, "Base layer missing parameter default curve");
                Assert.AreEqual(obj.activeSelf ? 1 : 0, baseParamCurve.keys[0].value);
            }
            else
            {
                Assert.That(andNode.Children[0], Is.EqualTo(new Constant(
                    wantState == obj.activeSelf
                )));
            }

            if (hasDriver)
            {
                Assert.AreEqual(initialExpr, orNode.Children[1], "missing initial expression: " + targetRule);
            }
            else
            {
                Assert.AreEqual(new Constant(false), orNode.Children[1],
                    "initial expression should be missing: " + targetRule);
            }
        }

        [Test]
        public void SplitIntoSubgraphs_GroupsByObjectTarget()
        {
            var obj = CreateChild(_root, "obj1");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj), new NullAction()));
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj), new NullAction()));
            graph.AddNode(new ReactionNode(new Constant(true), new NullAction()));

            var subgraphs = (List<ReactionGraph>)SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(2, subgraphs.Count);
            Assert.AreEqual(2, subgraphs[0].Nodes.Count);
            Assert.AreEqual(1, subgraphs[1].Nodes.Count);
        }

        [Test]
        public void SplitIntoSubgraphs_GroupsByParameterName()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ParameterExpression("p"), new NullAction()));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("p"), new NullAction()));
            graph.AddNode(new ReactionNode(new Constant(true), new NullAction()));

            var subgraphs = (List<ReactionGraph>)SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(2, subgraphs.Count);
            Assert.AreEqual(2, subgraphs[0].Nodes.Count);
            Assert.AreEqual(1, subgraphs[1].Nodes.Count);
        }

        [Test]
        public void SplitIntoSubgraphs_GroupsByActionTarget()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveParameter("p1", 1f)));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p2", true)));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveParameter("p1", 1f)));

            var subgraphs = (List<ReactionGraph>)SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(2, subgraphs.Count);
            Assert.AreEqual(2, subgraphs[0].Nodes.Count);
            Assert.AreEqual(1, subgraphs[1].Nodes.Count);
        }

        [Test]
        public void SplitIntoSubgraphs_IndependentNodes()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(CreateChild(_root, "a")), new NullAction()));
            graph.AddNode(new ReactionNode(new ParameterExpression("p1"), new DriveParameter("p2", 0.5f)));
            graph.AddNode(new ReactionNode(new Constant(true), new NullAction()));

            var subgraphs = (List<ReactionGraph>)SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(3, subgraphs.Count);
            Assert.That(subgraphs.All(g => g.Nodes.Count == 1));
        }

        [Test]
        public void SplitIntoSubgraphs_GroupsByDriveInternalParameterEffect()
        {
            // A node whose *effect* drives internal parameter "p" and a node whose *condition*
            // reads "p" must land in the same subgraph so their latencies can be aligned.
            // Before the fix, only expression-side parameter names were collected, so a writer
            // node with a Constant condition was not linked to its reader.
            var graph = new ReactionGraph();
            var writer = new ReactionNode(new Constant(true), new DriveInternalParameter("p", true));
            var reader = new ReactionNode(new InternalParameterCondition("p"), new NullAction());
            var independent = new ReactionNode(new Constant(true), new NullAction());

            graph.AddNode(writer);
            graph.AddNode(reader);
            graph.AddNode(independent);

            var subgraphs = (List<ReactionGraph>)SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(2, subgraphs.Count);
            var writerGroup = subgraphs.FirstOrDefault(g => g.Nodes.Contains(writer));
            Assert.IsNotNull(writerGroup);
            Assert.That(writerGroup.Nodes, Contains.Item(reader),
                "Writer and reader of the same internal parameter must be in the same subgraph");
        }

        // ── ProcessExternalObjectStateInputsTransform additional cases ────────

        [Test]
        public void ProcessExternalOAS_NotDrivenMode_ExpressionPassedThrough()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();
            var expr = new ObjectActiveState(obj, ObjectActiveState.State.NotDriven);
            graph.AddNode(new ReactionNode(expr, new NullAction()));

            ProcessExternalObjectStateInputsTransform.Apply(graph, _bakeContext);

            Assert.AreEqual(expr, graph.Nodes[0].Expression,
                "NotDriven OAS must not be substituted — it represents the RC-controlled state");
        }

        [Test]
        public void ProcessExternalOAS_NoExternalAnimation_BaseLayerNotWritten()
        {
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(obj, ObjectActiveState.State.Active),
                new NullAction()
            ));

            ProcessExternalObjectStateInputsTransform.Apply(graph, _bakeContext);

            var path = _asc.ObjectPathRemapper.GetVirtualPathForObject(obj);
            Assert.IsNull(
                _bakeContext.BaseLayerClip.GetFloatCurve(
                    EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive")),
                "Base layer clip must not be written when no external animation drives the object");
        }

        [Test]
        public void ProcessExternalOAS_ObjectNotReferencedByOAS_BaseLayerNotWritten()
        {
            var obj = CreateChild(_root, "obj");

            // Add an external animation clip that drives obj's active state.
            var controller = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            var layer = controller.AddLayer(LayerPriority.Default, "layer");
            var state = layer.StateMachine!.AddState("state");
            layer.StateMachine.DefaultState = state;
            var clip = VirtualClip.Create("clip");
            clip.SetFloatCurve(
                EditorCurveBinding.FloatCurve(
                    _asc.ObjectPathRemapper.GetVirtualPathForObject(obj),
                    typeof(GameObject), "m_IsActive"),
                AnimationCurve.Constant(0, 1, 0));
            state.Motion = clip;
            _asc.ControllerContext.Controllers["xyzzy"] = controller;

            // The graph has no node referencing obj via ObjectActiveState.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new NullAction()));

            ProcessExternalObjectStateInputsTransform.Apply(graph, _bakeContext);

            var path = _asc.ObjectPathRemapper.GetVirtualPathForObject(obj);
            Assert.IsNull(
                _bakeContext.BaseLayerClip.GetFloatCurve(
                    EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive")),
                "Base layer clip must not be written for objects never referenced by OAS");
        }

        [Test]
        public void ProcessExternalOAS_MultipleObjectsWithExternalAnimations_GetSeparateParameters()
        {
            var obj1 = CreateChild(_root, "obj1");
            var obj2 = CreateChild(_root, "obj2");

            var controller = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            var layer = controller.AddLayer(LayerPriority.Default, "layer");
            var state = layer.StateMachine!.AddState("state");
            layer.StateMachine.DefaultState = state;
            var clip = VirtualClip.Create("clip");
            foreach (var obj in new[] { obj1, obj2 })
            {
                clip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve(
                        _asc.ObjectPathRemapper.GetVirtualPathForObject(obj),
                        typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1, 0));
            }

            state.Motion = clip;
            _asc.ControllerContext.Controllers["xyzzy"] = controller;

            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj1, ObjectActiveState.State.Active),
                new NullAction()));
            graph.AddNode(new ReactionNode(new ObjectActiveState(obj2, ObjectActiveState.State.Active),
                new NullAction()));

            ProcessExternalObjectStateInputsTransform.Apply(graph, _bakeContext);

            // Both should be rewritten to OrNode at the top level.
            var or0 = graph.Nodes[0].Expression as OrNode;
            var or1 = graph.Nodes[1].Expression as OrNode;
            Assert.IsNotNull(or0);
            Assert.IsNotNull(or1);

            var pe0 = ((AndNode)or0.Children[0]).Children[0] as ParameterExpression;
            var pe1 = ((AndNode)or1.Children[0]).Children[0] as ParameterExpression;
            Assert.IsNotNull(pe0);
            Assert.IsNotNull(pe1);
            Assert.AreNotEqual(pe0.ParameterName, pe1.ParameterName,
                "Each object with external animation must receive its own parameter");
        }

        // ── SplitIntoSubgraphsTransform additional cases ──────────────────────

        [Test]
        public void SplitIntoSubgraphs_EmptyGraph_ReturnsEmptyList()
        {
            var graph = new ReactionGraph();
            var subgraphs = SplitIntoSubgraphsTransform.Apply(graph);
            Assert.AreEqual(0, subgraphs.Count);
        }

        [Test]
        public void SplitIntoSubgraphs_NestedAndExpression_CollectsObjectKey()
        {
            // OAS nested inside an AndNode must still link the two nodes into the same subgraph.
            var obj = CreateChild(_root, "obj");
            var graph = new ReactionGraph();
            var nodeA = new ReactionNode(
                new AndNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new Constant(true)),
                new NullAction()
            );
            var nodeB = new ReactionNode(new ObjectActiveState(obj, ObjectActiveState.State.Active), new NullAction());
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);

            var subgraphs = SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(1, subgraphs.Count, "Nodes sharing an OAS key nested in AND should be grouped");
            Assert.That(subgraphs[0].Nodes, Contains.Item(nodeA));
            Assert.That(subgraphs[0].Nodes, Contains.Item(nodeB));
        }

        [Test]
        public void SplitIntoSubgraphs_NestedNotExpression_CollectsParameterKey()
        {
            // IPC nested inside a NotNode must still register the parameter name as a grouping key.
            var graph = new ReactionGraph();
            var nodeA = new ReactionNode(
                new NotNode(new InternalParameterCondition("p")),
                new NullAction()
            );
            var nodeB = new ReactionNode(new Constant(true), new DriveInternalParameter("p", true));
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);

            var subgraphs = SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(1, subgraphs.Count, "Nodes sharing a parameter key nested in NOT should be grouped");
        }

        [Test]
        public void SplitIntoSubgraphs_TransitiveGrouping_ThreeNodesViaDistinctKeys()
        {
            // A reads "q" and writes "p"; B reads "p"; C writes "q".
            // A-B share key "p"; A-C share key "q" → all three in one subgraph.
            var graph = new ReactionGraph();
            var nodeA = new ReactionNode(new InternalParameterCondition("q"), new DriveInternalParameter("p", true));
            var nodeB = new ReactionNode(new InternalParameterCondition("p"), new NullAction());
            var nodeC = new ReactionNode(new Constant(true), new DriveInternalParameter("q", true));
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);

            var subgraphs = SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(1, subgraphs.Count, "All three nodes should be in one subgraph via transitive key sharing");
        }

        [Test]
        public void SplitIntoSubgraphs_MultipleWritersSameParameter_AllGrouped()
        {
            // Two writers and one reader of the same internal parameter.
            var graph = new ReactionGraph();
            var writer1 = new ReactionNode(new Constant(true), new DriveInternalParameter("p", true));
            var writer2 = new ReactionNode(new Constant(false), new DriveInternalParameter("p", false));
            var reader = new ReactionNode(new InternalParameterCondition("p"), new NullAction());
            graph.AddNode(writer1);
            graph.AddNode(writer2);
            graph.AddNode(reader);

            var subgraphs = SplitIntoSubgraphsTransform.Apply(graph);

            Assert.AreEqual(1, subgraphs.Count, "Both writers and the reader must be in the same subgraph");
        }
    }
}