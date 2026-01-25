using System;
using System.Runtime.Remoting;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
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
            _bakeContext = new BakeContext(_asc, vac);
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
            
            graph.ProcessExternalObjectStateInputs(_bakeContext);

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
    }
}