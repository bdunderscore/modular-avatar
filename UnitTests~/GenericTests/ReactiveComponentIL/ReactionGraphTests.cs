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

            var initialExpr = new ObjectActiveState(obj, wantState ? ObjectActiveState.State.Active : ObjectActiveState.State.Inactive);
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
                    wantState ? ParameterExpression.ConditionMode.GreaterThan : ParameterExpression.ConditionMode.LessThan,
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
                Assert.AreEqual(new Constant(false), orNode.Children[1], "initial expression should be missing: " + targetRule);
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
    }
}