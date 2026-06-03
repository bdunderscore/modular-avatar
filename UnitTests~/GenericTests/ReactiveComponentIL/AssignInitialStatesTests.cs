using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using nadena.dev.ndmf.animator;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class AssignInitialStatesTests : TestBase
    {
        private BakeContext _bakeContext;
        private AnimatorServicesContext _asc;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            var root = CreateRoot("root");
            var bc = CreateContext(root);
            _asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var vac = VirtualAnimatorController.Create(_asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, vac);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private float ContextValue(string param) => _bakeContext.GetParameterInitialValue(param);

        // ── basic driver activation ───────────────────────────────────────────

        [Test]
        public void EmptyGraph_NoError()
        {
            var graph = new ReactionGraph();
            Assert.DoesNotThrow(() => AssignInitialStates.ProcessGraph(_bakeContext, graph));
        }

        [Test]
        public void NoDriverEffects_NoContextChanges()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new NullAction()));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            // No DriveInternalParameter effects → context untouched
            Assert.AreEqual(0f, ContextValue("p"));
        }

        [Test]
        public void ConstantTrue_SetsParameterToTrue()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", true)));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(1f, ContextValue("p"));
        }

        [Test]
        public void ConstantTrue_DrivesFalseState_SetsParameterToZero()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", false)));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(0f, ContextValue("p"));
        }

        [Test]
        public void ConstantFalse_DriverNotActivated_ParameterRemainsDefault()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(false), new DriveInternalParameter("p", true)));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            // Condition is false → driver never fires → parameter not explicitly set
            Assert.AreEqual(0f, ContextValue("p"));
        }

        // ── external parameter conditions ────────────────────────────────────

        [Test]
        public void ExternalParameterCondition_TreatedAsFalse_DriverNotActivated()
        {
            // ParameterExpression (external) evaluates to false during initial-state analysis
            // because external parameter values are unknowable at build time.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new ParameterExpression("externalParam"),
                new DriveInternalParameter("p", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(0f, ContextValue("p"));
        }

        // ── initial BakeContext value as seed ────────────────────────────────

        [Test]
        public void InternalParameterCondition_ReadsExistingContextValue_ActivatesDriver()
        {
            // Pre-seed "a" in the context so the condition evaluates true.
            _bakeContext.SetParameter("a", 1f);

            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("a"),
                new DriveInternalParameter("b", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(1f, ContextValue("b"));
        }

        [Test]
        public void InternalParameterCondition_ContextValueZero_DriverNotActivated()
        {
            // "a" defaults to 0 in the context → condition false → driver inactive.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("a"),
                new DriveInternalParameter("b", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(0f, ContextValue("b"));
        }

        // ── fixed-point iteration (chained drivers) ───────────────────────────

        [Test]
        public void Chain_BDrivesA_BothSetCorrectly()
        {
            // B: Constant(true) → DriveInternalParameter("B", true)
            // A: InternalParameterCondition("B") → DriveInternalParameter("A", true)
            //
            // First pass: B=true, A not yet set (B not in stateOverrides yet).
            // Second pass: B=true in stateOverrides → A=true.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("B", true)));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("B"),
                new DriveInternalParameter("A", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(1f, ContextValue("B"), "B should be set to 1");
            Assert.AreEqual(1f, ContextValue("A"), "A should be set to 1 once B is resolved");
        }

        [Test]
        public void Chain_InvertedState_PropagatesCorrectly()
        {
            // B: Constant(true) → DriveInternalParameter("B", true)   → B = 1
            // A: InternalParameterCondition("B") → DriveInternalParameter("A", false) → A = 0
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("B", true)));
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("B"),
                new DriveInternalParameter("A", false)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(1f, ContextValue("B"));
            Assert.AreEqual(0f, ContextValue("A"));
        }

        // ── multiple drivers for same target ─────────────────────────────────

        [Test]
        public void TwoDriversSameTarget_LastActiveOneWins()
        {
            // Both conditions are true; the second driver in iteration order (false) wins.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", true)));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", false)));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(0f, ContextValue("p"), "Last driver in iteration order should win");
        }

        // ── compound expressions ──────────────────────────────────────────────

        [Test]
        public void AndCondition_BothTrue_DriverActivated()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new AndNode(new Constant(true), new Constant(true)),
                new DriveInternalParameter("p", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(1f, ContextValue("p"));
        }

        [Test]
        public void AndCondition_OneFalse_DriverNotActivated()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new AndNode(new Constant(true), new Constant(false)),
                new DriveInternalParameter("p", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(0f, ContextValue("p"));
        }

        [Test]
        public void OrCondition_OneFalse_DriverStillActivated()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new OrNode(new Constant(false), new Constant(true)),
                new DriveInternalParameter("p", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(1f, ContextValue("p"));
        }

        [Test]
        public void NotCondition_InvertsTrue_DriverNotActivated()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new NotNode(new Constant(true)),
                new DriveInternalParameter("p", true)
            ));

            AssignInitialStates.ProcessGraph(_bakeContext, graph);

            Assert.AreEqual(0f, ContextValue("p"));
        }
    }
}
