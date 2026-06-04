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
    /// <summary>
    /// Tests for EffectGroup.Emit() / EmitCondition — verifies that each IExpression type
    /// produces the correct motion-node tree structure.
    ///
    /// Structure contract after Emit():
    ///   rootNode  →  ProxyNode(_node)
    ///                  └─ Target = EmitCondition result
    ///                       ├─ BranchNode  (for parameter/IPC conditions)
    ///                       ├─ ProxyNode → MotionNode  (for Constant(true))
    ///                       └─ ProxyNode → EmptyNode   (for Constant(false))
    ///
    /// The Resolve() helper strips ProxyNode wrappers to reach the concrete node.
    /// </summary>
    public class EffectGroupEmitTests : TestBase
    {
        private BakeContext _bakeContext;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            var root = CreateRoot("root");
            var bc = CreateContext(root);
            var asc = bc.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var vac = VirtualAnimatorController.Create(asc.ControllerContext.CloneContext);
            _bakeContext = new BakeContext(bc, vac);
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private IMotionNode Resolve(IMotionNode node)
        {
            while (node is ProxyNode px) node = px.Target;
            return node;
        }

        private bool ResolvesToEffect(IMotionNode node) => Resolve(node) is MotionNode;
        private bool ResolvesToEmpty(IMotionNode node) => Resolve(node) is EmptyNode;

        private EffectGroup MakeGroup(IExpression condition)
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(condition, new DriveInternalParameter("effect", true)));
            var groups = AlignNodesTransform.CreateEffectGroups(_bakeContext, graph);
            return groups[new InternalParameterTarget("effect")];
        }

        private EffectGroup MakeGroup(params IExpression[] conditions)
        {
            var graph = new ReactionGraph();
            foreach (var condition in conditions)
            {
                graph.AddNode(new ReactionNode(condition, new DriveInternalParameter("effect", true)));
            }

            var groups = AlignNodesTransform.CreateEffectGroups(_bakeContext, graph);
            return groups[new InternalParameterTarget("effect")];
        }

        private void AssertBranchCondition(
            ProxyCondition proxy,
            string parameter,
            IMotionNode onTrue,
            IMotionNode onFalse)
        {
            var branch = proxy.Node as BranchNode;
            Assert.IsNotNull(branch, $"{parameter} condition should emit a BranchNode");
            Assert.AreEqual(parameter, branch.Parameter);
            Assert.AreSame(onTrue, branch.OnGreaterEquals);
            Assert.AreSame(onFalse, branch.OnLessThan);
        }

        // ── PriorityNode groups ───────────────────────────────────────────────

        [Test]
        public void ThreeNodes_EmitUsesPriorityNodeAndFillsEachProxyCondition()
        {
            var root = MakeGroup(
                new InternalParameterCondition("first"),
                new InternalParameterCondition("second"),
                new InternalParameterCondition("third")
            ).Emit();

            var priority = root as PriorityNode;
            Assert.IsNotNull(priority, "Three or more effects should emit through a PriorityNode");
            Assert.AreEqual(3, priority.Conditions.Count);

            foreach (var (_, motion) in priority.Conditions)
            {
                Assert.IsInstanceOf<MotionNode>(motion, "Each priority entry should keep its effect motion");
            }

            AssertBranchCondition(
                priority.Conditions[0].Item1,
                "first",
                priority.Conditions[0].Item1.OnTrueProxy,
                priority.Conditions[0].Item1.OnFalseProxy);
            AssertBranchCondition(
                priority.Conditions[1].Item1,
                "second",
                priority.Conditions[1].Item1.OnTrueProxy,
                priority.Conditions[1].Item1.OnFalseProxy);
            AssertBranchCondition(
                priority.Conditions[2].Item1,
                "third",
                priority.Conditions[2].Item1.OnTrueProxy,
                priority.Conditions[2].Item1.OnFalseProxy);
        }

        // ── Constant conditions ───────────────────────────────────────────────

        [Test]
        public void Constant_True_ResolvesToEffectMotion()
        {
            var root = MakeGroup(new Constant(true)).Emit();
            Assert.IsTrue(ResolvesToEffect(root),
                "Constant(true) should resolve to the effect MotionNode without any BranchNode");
        }

        [Test]
        public void Constant_False_ResolvesToEmpty()
        {
            var root = MakeGroup(new Constant(false)).Emit();
            Assert.IsTrue(ResolvesToEmpty(root),
                "Constant(false) should resolve to EmptyNode without any BranchNode");
        }

        // ── InternalParameterCondition ────────────────────────────────────────

        [Test]
        public void InternalParameterCondition_GreaterEquals_IsEffect_LessThan_IsEmpty()
        {
            var root = MakeGroup(new InternalParameterCondition("cond")).Emit();
            var branch = Resolve(root) as BranchNode;
            Assert.IsNotNull(branch, "IPC should produce a BranchNode");
            Assert.AreEqual("cond", branch.Parameter);
            Assert.IsTrue(ResolvesToEffect(branch.OnGreaterEquals),
                "OnGreaterEquals (condition true) should be the effect");
            Assert.IsTrue(ResolvesToEmpty(branch.OnLessThan),
                "OnLessThan (condition false) should be empty");
        }

        // ── NotNode ───────────────────────────────────────────────────────────

        [Test]
        public void NotNode_WrappingIPC_SwapsEffectAndEmpty()
        {
            var root = MakeGroup(new NotNode(new InternalParameterCondition("cond"))).Emit();
            var branch = Resolve(root) as BranchNode;
            Assert.IsNotNull(branch, "NOT(IPC) should produce a BranchNode");
            Assert.AreEqual("cond", branch.Parameter);
            Assert.IsTrue(ResolvesToEmpty(branch.OnGreaterEquals),
                "cond=true → NOT is false → empty");
            Assert.IsTrue(ResolvesToEffect(branch.OnLessThan),
                "cond=false → NOT is true → effect");
        }

        [Test]
        public void NotNode_WrappingConstantTrue_ResolvesToEmpty()
        {
            var root = MakeGroup(new NotNode(new Constant(true))).Emit();
            Assert.IsTrue(ResolvesToEmpty(root), "NOT(true) → empty");
        }

        [Test]
        public void NotNode_WrappingConstantFalse_ResolvesToEffect()
        {
            var root = MakeGroup(new NotNode(new Constant(false))).Emit();
            Assert.IsTrue(ResolvesToEffect(root), "NOT(false) → effect");
        }

        // ── OrNode ────────────────────────────────────────────────────────────

        [Test]
        public void OrNode_TwoIPCs_ShortCircuits_BFirstThenA()
        {
            // OR(a, b): emitted as BranchNode("b"): b=true → effect; b=false → BranchNode("a"): a=true → effect; else → empty.
            var root = MakeGroup(new OrNode(
                new InternalParameterCondition("a"),
                new InternalParameterCondition("b")
            )).Emit();

            var outer = Resolve(root) as BranchNode;
            Assert.IsNotNull(outer);
            Assert.AreEqual("b", outer.Parameter);
            Assert.IsTrue(ResolvesToEffect(outer.OnGreaterEquals), "b=true → OR is true → effect");

            var inner = Resolve(outer.OnLessThan) as BranchNode;
            Assert.IsNotNull(inner, "b=false path should check 'a'");
            Assert.AreEqual("a", inner.Parameter);
            Assert.IsTrue(ResolvesToEffect(inner.OnGreaterEquals), "b=false, a=true → OR is true → effect");
            Assert.IsTrue(ResolvesToEmpty(inner.OnLessThan), "both false → empty");
        }

        [Test]
        public void OrNode_TrueOrFalse_ResolvesToEffect()
        {
            var root = MakeGroup(new OrNode(new Constant(true), new Constant(false))).Emit();
            Assert.IsTrue(ResolvesToEffect(root));
        }

        [Test]
        public void OrNode_FalseOrFalse_ResolvesToEmpty()
        {
            var root = MakeGroup(new OrNode(new Constant(false), new Constant(false))).Emit();
            Assert.IsTrue(ResolvesToEmpty(root));
        }

        // ── AndNode ───────────────────────────────────────────────────────────

        [Test]
        public void AndNode_TwoIPCs_ShortCircuits_BFirstThenA()
        {
            // AND(a, b): BranchNode("b"): b=false → empty; b=true → BranchNode("a"): a=true → effect; else → empty.
            var root = MakeGroup(new AndNode(
                new InternalParameterCondition("a"),
                new InternalParameterCondition("b")
            )).Emit();

            var outer = Resolve(root) as BranchNode;
            Assert.IsNotNull(outer);
            Assert.AreEqual("b", outer.Parameter);
            Assert.IsTrue(ResolvesToEmpty(outer.OnLessThan), "b=false → AND is false → empty");

            var inner = Resolve(outer.OnGreaterEquals) as BranchNode;
            Assert.IsNotNull(inner, "b=true path should check 'a'");
            Assert.AreEqual("a", inner.Parameter);
            Assert.IsTrue(ResolvesToEffect(inner.OnGreaterEquals), "both true → effect");
            Assert.IsTrue(ResolvesToEmpty(inner.OnLessThan), "b=true, a=false → empty");
        }

        [Test]
        public void AndNode_TrueAndTrue_ResolvesToEffect()
        {
            var root = MakeGroup(new AndNode(new Constant(true), new Constant(true))).Emit();
            Assert.IsTrue(ResolvesToEffect(root));
        }

        [Test]
        public void AndNode_TrueAndFalse_ResolvesToEmpty()
        {
            var root = MakeGroup(new AndNode(new Constant(true), new Constant(false))).Emit();
            Assert.IsTrue(ResolvesToEmpty(root));
        }

        // ── ParameterExpression ───────────────────────────────────────────────

        [Test]
        public void ParameterExpression_GreaterThan_EffectWhenAboveThreshold()
        {
            var root = MakeGroup(
                new ParameterExpression("p", 0.5f, ParameterExpression.ConditionMode.GreaterThan)
            ).Emit();
            var branch = Resolve(root) as BranchNode;
            Assert.IsNotNull(branch);
            Assert.AreEqual("p", branch.Parameter);
            Assert.AreEqual(0.5f, branch.Threshold, 1e-5f);
            Assert.IsTrue(ResolvesToEffect(branch.OnGreaterEquals), "p >= 0.5 → condition true → effect");
            Assert.IsTrue(ResolvesToEmpty(branch.OnLessThan), "p < 0.5 → condition false → empty");
        }

        [Test]
        public void ParameterExpression_LessThan_EffectWhenBelowThreshold()
        {
            var root = MakeGroup(
                new ParameterExpression("p", 0.3f, ParameterExpression.ConditionMode.LessThan)
            ).Emit();
            var branch = Resolve(root) as BranchNode;
            Assert.IsNotNull(branch);
            Assert.AreEqual("p", branch.Parameter);
            Assert.AreEqual(0.3f, branch.Threshold, 1e-5f);
            Assert.IsTrue(ResolvesToEffect(branch.OnLessThan), "p < 0.3 → condition true → effect");
            Assert.IsTrue(ResolvesToEmpty(branch.OnGreaterEquals), "p >= 0.3 → condition false → empty");
        }

        [Test]
        public void ParameterExpression_CustomThreshold_PreservedInBranchNode()
        {
            var root = MakeGroup(new ParameterExpression("p", 0.75f)).Emit();
            var branch = Resolve(root) as BranchNode;
            Assert.IsNotNull(branch);
            Assert.AreEqual(0.75f, branch.Threshold, 1e-5f,
                "Custom threshold must be forwarded to BranchNode");
        }
    }
}
