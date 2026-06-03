using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using NUnit.Framework;

namespace UnitTestsReactiveComponentIL
{
    public class PruneUnusedInternalParametersTransformTests : TestBase
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static bool HasEffect(ReactionGraph g, string paramName) =>
            g.Nodes.Any(n =>
                n.Effects[0].TargetKey is InternalParameterTarget ipt &&
                ipt.ParameterName == paramName);

        // ── no-op cases ───────────────────────────────────────────────────────

        [Test]
        public void EmptyGraph_NoError()
        {
            var graph = new ReactionGraph();
            Assert.DoesNotThrow(() => PruneUnusedInternalParametersTransform.Apply(graph));
            Assert.AreEqual(0, graph.Nodes.Count);
        }

        [Test]
        public void NoInternalParameters_NoChanges()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new NullAction()));
            graph.AddNode(new ReactionNode(new ParameterExpression("ext"), new NullAction()));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.AreEqual(2, graph.Nodes.Count);
        }

        // ── direct reachability ───────────────────────────────────────────────

        [Test]
        public void InternalParamDirectlyUsedByExternalEffect_Kept()
        {
            // "p" is referenced in the condition of a node with external (NullAction) effect.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("p"), new NullAction()));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "p"), "Driver for 'p' should be kept");
        }

        [Test]
        public void InternalParamNotUsedByAnything_Pruned()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("unused", true)));
            // No external-effect node references "unused".

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsFalse(HasEffect(graph, "unused"), "Unreferenced driver should be pruned");
            Assert.AreEqual(0, graph.Nodes.Count);
        }

        [Test]
        public void MixedGraph_OnlyUnusedParameterPruned()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("used", true)));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("unused", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("used"), new NullAction()));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "used"), "Driver for 'used' should be kept");
            Assert.IsFalse(HasEffect(graph, "unused"), "Driver for 'unused' should be pruned");
        }

        // ── transitive reachability ───────────────────────────────────────────

        [Test]
        public void TransitiveChain_AllKept()
        {
            // External effect reads A → A driver reads B → both kept.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("B", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("B"), new DriveInternalParameter("A", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("A"), new NullAction()));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "A"), "A driver should be kept (read by external effect)");
            Assert.IsTrue(HasEffect(graph, "B"), "B driver should be kept (transitively needed for A)");
        }

        [Test]
        public void TransitiveChain_UnreferencedTailPruned()
        {
            // External effect reads A → A driver reads B → B driver reads C.
            // C is NOT referenced by any external-effect node, and B is NOT referenced
            // by the external effect directly; only A is. So B and C are transitively needed.
            // But add an extra "orphan" parameter D that nobody reads.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("C", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("C"), new DriveInternalParameter("B", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("B"), new DriveInternalParameter("A", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("A"), new NullAction()));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("D", true)));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "A"), "A kept");
            Assert.IsTrue(HasEffect(graph, "B"), "B kept (transitively needed)");
            Assert.IsTrue(HasEffect(graph, "C"), "C kept (transitively needed)");
            Assert.IsFalse(HasEffect(graph, "D"), "D pruned (not reachable)");
        }

        [Test]
        public void CascadingPrune_UnreferencedChainAllPruned()
        {
            // "X" drives "Y" drives "Z"; none are referenced by external effects.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("X", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("X"), new DriveInternalParameter("Y", true)));
            graph.AddNode(new ReactionNode(new InternalParameterCondition("Y"), new DriveInternalParameter("Z", true)));
            // No external-effect node reads X, Y, or Z.

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsFalse(HasEffect(graph, "X"), "X pruned");
            Assert.IsFalse(HasEffect(graph, "Y"), "Y pruned");
            Assert.IsFalse(HasEffect(graph, "Z"), "Z pruned");
            Assert.AreEqual(0, graph.Nodes.Count);
        }

        // ── graph with all-internal effects ──────────────────────────────────

        [Test]
        public void AllInternalEffects_NoExternalRoots_AllPruned()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("a", true)));
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("b", false)));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.AreEqual(0, graph.Nodes.Count);
        }

        // ── compound conditions in external-effect nodes ──────────────────────

        [Test]
        public void InternalParamInAndExpression_Kept()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", true)));
            graph.AddNode(new ReactionNode(
                new AndNode(new InternalParameterCondition("p"), new Constant(true)),
                new NullAction()
            ));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "p"));
        }

        [Test]
        public void InternalParamInOrExpression_Kept()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", true)));
            graph.AddNode(new ReactionNode(
                new OrNode(new InternalParameterCondition("p"), new Constant(false)),
                new NullAction()
            ));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "p"));
        }

        [Test]
        public void InternalParamInNotExpression_Kept()
        {
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new DriveInternalParameter("p", true)));
            graph.AddNode(new ReactionNode(
                new NotNode(new InternalParameterCondition("p")),
                new NullAction()
            ));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsTrue(HasEffect(graph, "p"));
        }

        // ── parameter that is both reader and driver ──────────────────────────

        [Test]
        public void ParameterUsedInOwnDriverCondition_StillPrunedIfExternallyUnreferenced()
        {
            // "p" drives itself (self-referential condition), but nothing external reads "p".
            // It should still be pruned because no externally-visible effect needs it.
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("p"),
                new DriveInternalParameter("p", true)
            ));

            PruneUnusedInternalParametersTransform.Apply(graph);

            Assert.IsFalse(HasEffect(graph, "p"), "Self-referential unreachable param should be pruned");
        }
    }
}
