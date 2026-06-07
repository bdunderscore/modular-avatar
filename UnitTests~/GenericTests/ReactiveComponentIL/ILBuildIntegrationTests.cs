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
using UnityEditor;

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

            Assert.IsTrue(objA.activeSelf,
                "SetBaseState must call Target.SetActive(true) — A is always driven active");
            Assert.IsFalse(_vac.Parameters.Keys.Any(k => k.Contains("ObjActive/A")),
                "ObjActive/A is not used as a condition so it must be cleaned up as an orphan");
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

            Assert.IsTrue(objA.activeSelf,
                "P=1.0 drives A active — SetBaseState must call Target.SetActive(true)");
            Assert.IsFalse(_vac.Parameters.Keys.Any(k => k.Contains("ObjActive/A")),
                "ObjActive/A is not used as a condition so it must be cleaned up as an orphan");
        }

        // ── SetBaseState / scene-state ordering ─────────────────────────────
        //
        // Intent (per spec):
        //   BaseLayerClip  = original activeSelf BEFORE RC correction (lets other FX layers
        //                    interact with the property when no RC effect is active)
        //   Scene object   = RC-evaluated initial state AFTER all rules are applied
        //                    (correct state when the animator is disabled by safety systems)
        //
        // DriveActiveState.SetBaseState handles both writes: it records activeSelf in
        // BaseLayerClip FIRST, then calls Target.SetActive(Active) when actionStartsActive=true.
        //
        // ObjActive/A is an orphan in these tests (nothing reads it as a condition), so
        // PruneOrphanedInternalParameters removes it from the VAC after the build.

        private float BaseLayerValue(GameObject obj)
        {
            var path = _bakeContext.ObjectPathRemapper.GetVirtualPathForObject(obj);
            var curve = _bakeContext.BaseLayerClip.GetFloatCurve(
                EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"));
            Assert.IsNotNull(curve, $"BaseLayerClip must have a curve for {obj.name}.m_IsActive");
            return curve.Evaluate(0);
        }

        private ReactionGraph MakeDriveActiveGraph(GameObject obj, bool driveToActive, string paramName)
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ParameterExpression(paramName, 0.5f, ParameterExpression.ConditionMode.GreaterThan),
                new DriveActiveState(obj, driveToActive)
            ));
            return graph;
        }

        // Parameters: (sceneActive, pDefault, expectedSceneActiveAfterBuild, expectedBaseLayerValue)
        private static readonly object[] SetBaseStateCases =
        {
            // inactive scene, rule fires    → SetBaseState(true)  → scene set active, base=0
            new object[] { false, 1.0f, true,  0f },
            // active scene,   rule fires    → SetBaseState(true)  → scene stays active, base=1
            new object[] { true,  1.0f, true,  1f },
            // inactive scene, rule inactive → SetBaseState(false) → scene stays inactive, base=0
            new object[] { false, 0.0f, false, 0f },
            // active scene,   rule inactive → SetBaseState(false) → scene stays active, base=1
            new object[] { true,  0.0f, true,  1f },
        };

        [Test, TestCaseSource(nameof(SetBaseStateCases))]
        public void SetBaseState_CorrectlyRecordsOriginalStateAndUpdatesScene(
            bool sceneActive, float pDefault, bool expectedSceneActive, float expectedBaseLayer)
        {
            var objA = CreateChild(_root, "A");
            objA.SetActive(sceneActive);

            _bakeContext.EnsureParameterPresent("P", pDefault);
            ILBuild.Build(_bakeContext, MakeDriveActiveGraph(objA, true, "P"));

            Assert.AreEqual(expectedSceneActive, objA.activeSelf,
                "Scene object active state must match the RC-evaluated initial state");
            Assert.AreEqual(expectedBaseLayer, BaseLayerValue(objA),
                "BaseLayerClip must record the ORIGINAL activeSelf before any RC correction");
            Assert.IsFalse(_vac.Parameters.Keys.Any(k => k.Contains("ObjActive/A")),
                "ObjActive/A is orphaned (nothing reads it) and must be cleaned up");
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

        // ── Non-orphan ObjActive tracking ────────────────────────────────────
        //
        // ForwardObjectActiveDriversTransform only forwards ObjectActiveState references
        // when driverCount ≤ 2. With 3 nodes driving A the reference is preserved, so
        // InternalParameterCondition("ObjActive/A") survives PruneUnused and the parameter
        // is kept in the VAC.
        //
        // 2×2 matrix: (A starts active/inactive) × (rule fires / doesn't fire)
        //
        //   ObjActive/A should always reflect A's ACTUAL active state:
        //   - When P=1 (rule fires): ObjActive/A=1, A set active by SetBaseState
        //   - When P=0, A inactive: ObjActive/A=0, A stays inactive
        //   - When P=0, A active: ObjActive/A=1 (A IS active via scene state)
        //     NOTE: this last case currently fails because DriveInternalParameter.SetBaseState
        //     resets the parameter to 0 when actionStartsActive=false, overwriting the correct
        //     EnsureParameter seed value.

        private static readonly object[] NonOrphanObjActiveCases =
        {
            // (aStartsActive, pDefault, expectedObjActiveA, expectedAActiveAfterBuild)
            new object[] { false, 0.0f, 0f, false },  // inactive, rule off  → stays inactive
            new object[] { false, 1.0f, 1f, true  },  // inactive, rule on   → set active
            new object[] { true,  1.0f, 1f, true  },  // active,   rule on   → stays active
            new object[] { true,  0.0f, 1f, true  },  // active,   rule off  → ObjActive/A must remain 1
        };

        [Test, TestCaseSource(nameof(NonOrphanObjActiveCases))]
        public void ObjActive_NonOrphan_CorrectlyTracksActualObjectActiveState(
            bool aStartsActive, float pDefault, float expectedObjActiveA, bool expectedAActive)
        {
            var objA = CreateChild(_root, "A");
            objA.SetActive(aStartsActive);
            var objB = CreateChild(_root, "B");
            objB.SetActive(false);

            _bakeContext.EnsureParameterPresent("P", pDefault);

            var graph = new ReactionGraph();

            // Three nodes drive A → driverCount=3 → ForwardObjectActiveDriversTransform skips
            // forwarding (it only forwards when driverCount ≤ 2), so ObjectActiveState(A) in
            // the second rule is preserved and becomes InternalParameterCondition("ObjActive/A").
            graph.AddNode(new ReactionNode(
                new ParameterExpression("P", 0.5f, ParameterExpression.ConditionMode.GreaterThan),
                new DriveActiveState(objA, true)
            ));
            graph.AddNode(new ReactionNode(new Constant(false), new DriveActiveState(objA, false)));
            graph.AddNode(new ReactionNode(new Constant(false), new DriveActiveState(objA, false)));

            // Second group reads A's active state — keeps ObjActive/A alive after pruning.
            graph.AddNode(new ReactionNode(
                new ObjectActiveState(objA, ObjectActiveState.State.Active),
                new DriveActiveState(objB, true)
            ));

            ILBuild.Build(_bakeContext, graph);

            var objActiveAName = _vac.Parameters.Keys.Single(k => k.Contains("ObjActive/A"));
            Assert.AreEqual(expectedObjActiveA, _bakeContext.GetParameterInitialValue(objActiveAName),
                "ObjActive/A must reflect A's actual active state (not just whether the RC rule fired)");
            Assert.AreEqual(expectedAActive, objA.activeSelf,
                "A's scene state must match the RC-evaluated initial state");
        }
    }
}
