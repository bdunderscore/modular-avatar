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
    public class ILBuildIntegrationTests : TestBase
    {
        private BakeContext _bakeContext;
        private GameObject _root;
        private AnimatorServicesContext _asc;
        private VirtualAnimatorController _vac;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _root = CreateRoot("root");
            var bc = CreateContext(_root);
            _asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            _vac = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, _vac);
        }

        [Test]
        public void Build_InactiveObjectDrivenActiveByAlwaysOnNode_StartsActive()
        {
            // End-to-end regression for the pipeline-ordering bug:
            // AssignInitialStates.ProcessGraph was called before ConvertToInternalParametersTransform,
            // so the fixpoint saw no DriveInternalParameter effects and was a complete no-op.
            // Object A is inactive in the scene (activeSelf=false), but an always-on node drives
            // it active. After ILBuild.Build the ObjActive/A parameter must be 1, not 0.
            var objA = CreateChild(_root, "A");
            objA.SetActive(false);

            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveActiveState(objA, true)));

            ILBuild.Build(_bakeContext, graph);

            var paramName = _vac.Parameters.Keys.Single(k => k.Contains("ObjActive/A"));
            Assert.AreEqual(1.0f, _bakeContext.GetParameterInitialValue(paramName),
                "A is inactive (activeSelf=false) but always driven active — initial value must be 1, not 0");
        }

        [Test]
        public void Build_InactiveObjectDrivenByExternalParamWithDefault_StartsActive()
        {
            // Regression: EnsureParameterPresent defaulted external parameters to 0, so
            // ProcessGraph evaluated ParameterExpression("P") = 0 > 0.5 = false and left
            // ObjActive/A at 0. The fix passes arg.InitialValue so P = 1.0 in context.
            //
            // Here we pre-register P with its correct default (1.0), simulating what
            // ConvertCondition does after the fix.
            var objA = CreateChild(_root, "A");
            objA.SetActive(false);

            _bakeContext.EnsureParameterPresent("P", 1.0f);

            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ParameterExpression("P", 0.5f, ParameterExpression.ConditionMode.GreaterThan),
                new DriveActiveState(objA, true)
            ));

            ILBuild.Build(_bakeContext, graph);

            var paramName = _vac.Parameters.Keys.Single(k => k.Contains("ObjActive/A"));
            Assert.AreEqual(1.0f, _bakeContext.GetParameterInitialValue(paramName),
                "A is inactive in scene but P=1.0 drives it active — ObjActive/A must start at 1");
        }

        [Test]
        public void Build_DelayParameters_InitializedFromExternalParamDefault()
        {
            // Graph design:
            //   3-case EG (Latency=2): reads Q, drives internal param "result"
            //   2-case EG (Latency=1): reads "result" AND Q, drives object A's active state
            //
            // Depth analysis (BFS from ExternalEffect=0):
            //   2-case EG depth=0, "result" depth=1, 3-case EG depth=1
            //   ExternalSource depth = 1 + Latency(3-case EG=2) = 3
            //   In the 2-case EG (depth=0, Latency=1): expectedParamDepth=1
            //     "result" depth=1 → delay=0 (no delay)
            //     Q (ExternalSource) depth=3 → delay=2 → creates Q/$1 and Q/$2
            //
            // Regression: those delay params were initialized to 0 because EnsureParameterPresent
            // defaulted Q to 0. With the fix, they must be initialized to Q's correct default (1.0).
            var objA = CreateChild(_root, "A");

            _bakeContext.EnsureParameterPresent("Q", 1.0f);

            var resultParam = _bakeContext.AddParameter("result", 0);

            var graph = new ReactionGraph();

            // 3-case EffectGroup: three nodes driving resultParam → PriorityNode → Latency=2
            graph.AddNode(new ReactionNode(
                new ParameterExpression("Q", 0.5f, ParameterExpression.ConditionMode.GreaterThan),
                new DriveInternalParameter(resultParam, true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(false),
                new DriveInternalParameter(resultParam, false)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(false),
                new DriveInternalParameter(resultParam, true)
            ));

            // 2-case EffectGroup: two nodes → ProxyCondition → Latency=1
            // Condition reads BOTH "result" (internal) AND Q (external), forcing a 2-frame delay on Q.
            graph.AddNode(new ReactionNode(
                new AndNode(
                    new InternalParameterCondition(resultParam),
                    new ParameterExpression("Q", 0.5f, ParameterExpression.ConditionMode.GreaterThan)
                ),
                new DriveActiveState(objA, true)
            ));
            graph.AddNode(new ReactionNode(
                new Constant(false),
                new DriveActiveState(objA, false)
            ));

            ILBuild.Build(_bakeContext, graph);

            var delayParams = _vac.Parameters.Keys
                .Where(AlignNodesTransform.IsDelayParam)
                .Where(k => AlignNodesTransform.DelayParamBaseName(k) == "Q")
                .ToList();

            Assert.IsNotEmpty(delayParams, "Delay parameters for Q must be created by AlignNodesTransform");
            foreach (var dp in delayParams)
            {
                Assert.AreEqual(1.0f, _bakeContext.GetParameterInitialValue(dp),
                    $"{dp} must start at Q's correct default (1.0), not 0 — " +
                    "a wrong initial value causes multi-frame flicker on avatar load");
            }
        }
    }
}
