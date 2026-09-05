#nullable enable

using System;
using modular_avatar_tests;
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.StaticProcessing;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace UnitTestsReactiveComponentIL
{
    public class StaticProcessingTests : TestBase
    {
        [Test]
        public void EarlierConstantTrueActionsAreRemovedAndOnlyFinalActionIsDispatched()
        {
            var target = new object();
            var earlier = new TestAction(target, StaticApplyResult.Applied);
            var final = new TestAction(target, StaticApplyResult.Applied);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), earlier));
            graph.AddNode(new ReactionNode(new Constant(true), final));

            StaticProcessing.Apply(graph);

            Assert.AreEqual(0, earlier.ApplyCount);
            Assert.AreEqual(1, final.ApplyCount);
            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.That(graph.Nodes[0].Effects.Single(), Is.TypeOf<AlreadyApplied>());
            Assert.AreSame(final, ((AlreadyApplied)graph.Nodes[0].Effects.Single()).Inner);
        }

        [Test]
        public void LaterNonconstantRuleRetainsEarlierConstantForBackendInitialState()
        {
            var target = new object();
            var initial = new TestAction(target, StaticApplyResult.Applied);
            var conditional = new TestAction(target, StaticApplyResult.Applied);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), initial));
            graph.AddNode(new ReactionNode(new ParameterExpression("P"), conditional));

            StaticProcessing.Apply(graph);

            Assert.AreEqual(0, initial.ApplyCount);
            Assert.AreEqual(0, conditional.ApplyCount);
            Assert.AreSame(initial, graph.Nodes[0].Effects.Single());
            Assert.AreSame(conditional, graph.Nodes[1].Effects.Single());
        }

        [Test]
        public void LaterConstantRuleRemovesEarlierNonconstantRule()
        {
            var target = new object();
            var conditional = new TestAction(target, StaticApplyResult.Applied);
            var final = new TestAction(target, StaticApplyResult.Applied);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new ParameterExpression("P"), conditional));
            graph.AddNode(new ReactionNode(new Constant(true), final));

            StaticProcessing.Apply(graph);

            Assert.AreEqual(0, conditional.ApplyCount);
            Assert.AreEqual(1, final.ApplyCount);
            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.AreSame(final, ((AlreadyApplied)graph.Nodes[0].Effects.Single()).Inner);
        }

        [Test]
        public void FalseAndNonconstantFinalActionsAreUntouched()
        {
            var falseAction = new TestAction(new object(), StaticApplyResult.Applied);
            var nonconstantAction = new TestAction(new object(), StaticApplyResult.Applied);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(false), falseAction));
            graph.AddNode(new ReactionNode(new ParameterExpression("P"), nonconstantAction));

            StaticProcessing.Apply(graph);

            Assert.AreEqual(0, falseAction.ApplyCount);
            Assert.AreEqual(0, nonconstantAction.ApplyCount);
            Assert.AreSame(falseAction, graph.Nodes[0].Effects.Single());
            Assert.AreSame(nonconstantAction, graph.Nodes[1].Effects.Single());
        }

        [Test]
        public void AppliedActionIsWrappedExactlyOnce()
        {
            var action = new TestAction(new object(), StaticApplyResult.Applied);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), action));

            StaticProcessing.Apply(graph);
            StaticProcessing.Apply(graph);

            Assert.AreEqual(1, action.ApplyCount);
            var applied = graph.Nodes.Single().Effects.Single() as AlreadyApplied;
            Assert.IsNotNull(applied);
            Assert.AreSame(action, applied!.Inner);
            Assert.IsFalse(applied.Inner is AlreadyApplied);
        }

        [Test]
        public void RetainedActionIsLeftUnwrapped()
        {
            var action = new TestAction(new object(), StaticApplyResult.Retain);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), action));

            StaticProcessing.Apply(graph);

            Assert.AreEqual(1, action.ApplyCount);
            Assert.AreSame(action, graph.Nodes.Single().Effects.Single());
        }

        [Test]
        public void MultipleMeshHidesOnOneRendererAreDeferredAndBatched()
        {
            var renderer = CreateRoot("renderer").AddComponent<SkinnedMeshRenderer>();
            var mesh = TrackObject(CreateMesh());
            renderer.sharedMesh = mesh;
            var firstSelector = new PrimitiveSelector(true, false);
            var secondSelector = new PrimitiveSelector(false, true);
            var firstTarget = MeshSectionTarget.ForMask(renderer, firstSelector);
            var secondTarget = MeshSectionTarget.ForMask(renderer, secondSelector);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new HideMeshSection(firstTarget, firstSelector)));
            graph.AddNode(new ReactionNode(new Constant(true), new HideMeshSection(secondTarget, secondSelector)));

            StaticProcessing.Apply(graph);

            Assert.IsEmpty(graph.Nodes);
            Assert.AreNotSame(mesh, renderer.sharedMesh,
                "Both removals should be committed together when the shared static context is disposed.");
            Assert.AreEqual(3, renderer.sharedMesh.triangles.Length);
        }

        [Test]
        public void SuccessfulShapeHideRemovesShapeEffects()
        {
            var renderer = CreateRoot("renderer").AddComponent<SkinnedMeshRenderer>();
            var target = MeshSectionTarget.ForShape(renderer, "smile");
            renderer.sharedMesh = TrackObject(CreateMesh("smile"));
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), new HideMeshSection(target, new PrimitiveSelector(true, true))));
            graph.AddNode(new ReactionNode(new ParameterExpression("P"), new SetShapeKey(renderer, "smile", 100f)));

            StaticProcessing.Apply(graph);

            Assert.IsEmpty(graph.Nodes);
        }

        [Test]
        public void AppliedRetainHideIsRemovedWithoutInvalidatingShapeEffects()
        {
            var renderer = CreateRoot("renderer").AddComponent<SkinnedMeshRenderer>();
            var target = MeshSectionTarget.ForShape(renderer, "smile");
            var shape = new SetShapeKey(renderer, "smile", 100f);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(
                new Constant(true),
                new AlreadyApplied(HideMeshSection.Retain(target))));
            graph.AddNode(new ReactionNode(new ParameterExpression("P"), shape));

            RemoveAppliedMeshHides.Apply(graph);

            Assert.AreEqual(1, graph.Nodes.Count);
            Assert.AreSame(shape, graph.Nodes.Single().Effects.Single());
        }

        [Test]
        public void FailedShapeHideNeitherInvalidatesNorDisappears()
        {
            var renderer = CreateRoot("renderer").AddComponent<SkinnedMeshRenderer>();
            var target = MeshSectionTarget.ForShape(renderer, "smile");
            var hide = new HideMeshSection(target, new PrimitiveSelector(true, true));
            var shape = new SetShapeKey(renderer, "smile", 100f);
            var graph = new ReactionGraph();
            graph.AddNode(new ReactionNode(new Constant(true), hide));
            graph.AddNode(new ReactionNode(new ParameterExpression("P"), shape));

            StaticProcessing.Apply(graph);

            Assert.AreSame(hide, graph.Nodes[0].Effects.Single());
            Assert.AreSame(shape, graph.Nodes[1].Effects.Single());
        }

        private static Mesh CreateMesh(string? shapeName = null)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, Vector3.one, Vector3.back
                },
                triangles = new[] { 0, 1, 2, 3, 4, 5 }
            };
            if (shapeName != null)
                mesh.AddBlendShapeFrame(shapeName, 100f, new Vector3[6], null, null);
            return mesh;
        }

        private sealed class TestAction : IAction
        {
            private readonly object _target;
            private readonly StaticApplyResult _result;
            public int ApplyCount { get; private set; }
            public object TargetKey => _target;

            public TestAction(object target, StaticApplyResult result)
            {
                _target = target;
                _result = result;
            }

            public bool ApproximatelyEqual(IAction other) => ReferenceEquals(this, other);

            public StaticApplyResult ApplyStatic(StaticApplyContext context)
            {
                ApplyCount++;
                return _result;
            }
        }

        private sealed class PrimitiveSelector : IMeshSelector
        {
            private readonly bool[] _mask;

            public PrimitiveSelector(params bool[] mask)
            {
                _mask = mask;
            }

            public bool Equals(IMeshSelector? other) => ReferenceEquals(this, other);

            public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
            {
                for (var index = 0; index < selectedPrimitives.Length; index++)
                    selectedPrimitives[index] = _mask[index];
                return default;
            }
        }
    }
}
