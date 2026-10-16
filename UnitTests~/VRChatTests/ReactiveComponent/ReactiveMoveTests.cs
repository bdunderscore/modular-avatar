#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

namespace nadena.dev.modular_avatar.test.ReactiveComponent
{
    internal class ReactiveMoveTests : TestBase
    {
        [Test]
        public void PositionAndRotationRules_CreateIndependentConstraints()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            toMove.transform.localPosition = new Vector3(1, 2, 3);
            toMove.transform.localEulerAngles = new Vector3(10, 20, 30);
            var firstTarget = CreateChild(root, "FirstTarget");
            var secondTarget = CreateChild(root, "SecondTarget");
    
            AddMove(root, "FirstRule", toMove, firstTarget, position: true, rotation: true, scale: false);
            AddMove(root, "SecondRule", toMove, secondTarget, position: true, rotation: true, scale: false);
    
            AvatarProcessor.ProcessAvatar(root);
    
            Assert.That(toMove.GetComponents<VRCParentConstraint>(), Is.Empty);
            var position = toMove.GetComponents<VRCPositionConstraint>().Single();
            var rotation = toMove.GetComponents<VRCRotationConstraint>().Single();
            AssertConstraintState(position);
            AssertConstraintState(rotation);
            AssertVector3(position.PositionAtRest, new Vector3(1, 2, 3));
            AssertVector3(rotation.RotationAtRest, new Vector3(10, 20, 30));
            AssertSources(position, firstTarget.transform, secondTarget.transform);
            AssertSources(rotation, firstTarget.transform, secondTarget.transform);
        }
    
        [Test]
        public void MixedPositionAndRotationRules_CreateSplitConstraints_WithCombinedRuleInBoth()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            toMove.transform.localPosition = new Vector3(3, 4, 5);
            toMove.transform.localEulerAngles = new Vector3(40, 50, 60);
            var positionTarget = CreateChild(root, "PositionTarget");
            var rotationTarget = CreateChild(root, "RotationTarget");
            var combinedTarget = CreateChild(root, "CombinedTarget");
    
            AddMove(root, "PositionRule", toMove, positionTarget, position: true, rotation: false, scale: false);
            AddMove(root, "RotationRule", toMove, rotationTarget, position: false, rotation: true, scale: false);
            AddMove(root, "CombinedRule", toMove, combinedTarget, position: true, rotation: true, scale: false);
    
            AvatarProcessor.ProcessAvatar(root);
    
            Assert.That(toMove.GetComponents<VRCParentConstraint>(), Is.Empty);
            var position = toMove.GetComponents<VRCPositionConstraint>().Single();
            var rotation = toMove.GetComponents<VRCRotationConstraint>().Single();
            AssertConstraintState(position);
            AssertConstraintState(rotation);
            AssertVector3(position.PositionAtRest, new Vector3(3, 4, 5));
            AssertVector3(position.PositionOffset, Vector3.zero);
            AssertVector3(rotation.RotationAtRest, new Vector3(40, 50, 60));
            AssertVector3(rotation.RotationOffset, Vector3.zero);
            AssertSources(position, positionTarget.transform, combinedTarget.transform);
            AssertSources(rotation, rotationTarget.transform, combinedTarget.transform);
        }
    
        [Test]
        public void ScaleRules_CreateIndependentScaleConstraint()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            toMove.transform.localScale = new Vector3(2, 3, 4);
            var scaleTarget = CreateChild(root, "ScaleTarget");
    
            AddMove(root, "ScaleRule", toMove, scaleTarget, position: false, rotation: false, scale: true);
    
            AvatarProcessor.ProcessAvatar(root);
    
            Assert.That(toMove.GetComponents<VRCParentConstraint>(), Is.Empty);
            Assert.That(toMove.GetComponents<VRCPositionConstraint>(), Is.Empty);
            Assert.That(toMove.GetComponents<VRCRotationConstraint>(), Is.Empty);
            var scale = toMove.GetComponents<VRCScaleConstraint>().Single();
            AssertConstraintState(scale);
            AssertVector3(scale.ScaleAtRest, new Vector3(2, 3, 4));
            AssertVector3(scale.ScaleOffset, Vector3.one);
            AssertSources(scale, scaleTarget.transform);
        }
    
        [Test]
        public void PositionWinnerClips_AnimateTheCompleteSourceAndGlobalWeightUnion()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var targets = Enumerable.Range(0, 21)
                .Select(index => CreateChild(root, $"Target{index}"))
                .ToList();
    
            for (var index = 0; index < targets.Count; index++)
                AddMove(root, $"Rule{index}", toMove, targets[index], position: true, rotation: false, scale: false);
    
            AvatarProcessor.ProcessAvatar(root);
    
            var constraint = toMove.GetComponents<VRCPositionConstraint>().Single();
            AssertSources(constraint, targets.Select(target => target.transform).ToArray());
            var path = "ToMove";
            var expectedProperties = Enumerable.Range(0, 21)
                .Select(SourceWeightProperty)
                .Append("GlobalWeight")
                .Append("FreezeToWorld")
                .ToList();
            var clips = GeneratedClips(root).ToList();
    
            foreach (var winner in new[] { 0, 15, 16, 20 })
            {
                var clip = clips.Single(clip => HasCurveValue(
                    clip, path, typeof(VRCPositionConstraint), SourceWeightProperty(winner), 1f));
                var properties = AnimationUtility.GetCurveBindings(clip)
                    .Where(binding => binding.path == path && binding.type == typeof(VRCPositionConstraint))
                    .Select(binding => binding.propertyName)
                    .ToList();
                CollectionAssert.AreEquivalent(expectedProperties, properties,
                    $"winner {winner} must write every source and GlobalWeight in one atomic clip");
    
                foreach (var sourceIndex in Enumerable.Range(0, 21))
                    Assert.That(CurveValue(clip, path, typeof(VRCPositionConstraint), SourceWeightProperty(sourceIndex)),
                        Is.EqualTo(sourceIndex == winner ? 1f : 0f));
                Assert.That(CurveValue(clip, path, typeof(VRCPositionConstraint), "GlobalWeight"), Is.EqualTo(1f));
            }
    
            Assert.That(SourceWeightProperty(0), Is.EqualTo("Sources.source0.Weight"));
            Assert.That(SourceWeightProperty(15), Is.EqualTo("Sources.source15.Weight"));
            Assert.That(SourceWeightProperty(16), Is.EqualTo("Sources.overflowList.Array.data[0].Weight"));
            Assert.That(SourceWeightProperty(20), Is.EqualTo("Sources.overflowList.Array.data[4].Weight"));
            Assert.That(constraint.Sources, Has.Count.EqualTo(21));
        }
    
        [Test]
        public void PositionAndRotationWinners_AreIndependent()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var positionTarget = CreateChild(root, "PositionTarget");
            var rotationTarget = CreateChild(root, "RotationTarget");
    
            AddMove(root, "PositionRule", toMove, positionTarget, position: true, rotation: false, scale: false);
            AddMove(root, "RotationRule", toMove, rotationTarget, position: false, rotation: true, scale: false);
    
            AvatarProcessor.ProcessAvatar(root);
    
            var clips = GeneratedClips(root).ToList();
            const string path = "ToMove";
            Assert.That(clips.Any(clip => HasCurveValue(
                clip, path, typeof(VRCPositionConstraint), SourceWeightProperty(0), 1f)), Is.True);
            Assert.That(clips.Any(clip => HasCurveValue(
                clip, path, typeof(VRCRotationConstraint), SourceWeightProperty(0), 1f)), Is.True);
        }
    
        [Test]
        public void InactiveMove_ResetsGlobalFreezeAndAllSourceWeights()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var target = CreateChild(root, "Target");
            AddMove(root, "Rule", toMove, target, position: true, rotation: false, scale: false);
    
            AvatarProcessor.ProcessAvatar(root);
    
            var clips = GeneratedClips(root).Where(clip =>
                HasCurveValue(clip, "ToMove", typeof(VRCPositionConstraint), "GlobalWeight", 0f));
            foreach (var clip in clips)
            {
                Assert.That(CurveValue(clip, "ToMove", typeof(VRCPositionConstraint), "FreezeToWorld"), Is.Zero);
                Assert.That(CurveValue(clip, "ToMove", typeof(VRCPositionConstraint), SourceWeightProperty(0)), Is.Zero);
            }
        }
    
        [Test]
        public void InactiveTransitionedMove_ReleasesItsTarget()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var target = CreateChild(root, "Target");
            var move = AddMove(root, "Rule", toMove, target, position: true, rotation: false, scale: false);
            move.TransitionTime = 1f;
    
            AvatarProcessor.ProcessAvatar(root);
    
            var clip = GeneratedClips(root).Single(clip =>
                HasCurveValue(clip, "ToMove", typeof(VRCPositionConstraint), "GlobalWeight", 0f));
            Assert.That(CurveValue(clip, "ToMove", typeof(VRCPositionConstraint), "FreezeToWorld"), Is.Zero);
            Assert.That(CurveValue(clip, "ToMove", typeof(VRCPositionConstraint), SourceWeightProperty(0)), Is.Zero);
            Assert.That(CurveValue(clip, "ToMove", typeof(VRCPositionConstraint), SourceWeightProperty(1)), Is.Zero);
        }
    
        [Test]
        public void FixedWorldMove_FreezesTheRequestedChannel()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var target = CreateChild(root, "Target");
            var move = AddMove(root, "Rule", toMove, target, position: true, rotation: false, scale: false);
            move.FixToWorld = true;
    
            AvatarProcessor.ProcessAvatar(root);
    
            var clip = GeneratedClips(root).Single(clip =>
                HasCurveValue(clip, "ToMove", typeof(VRCPositionConstraint), "FreezeToWorld", 1f));
            Assert.That(CurveValue(clip, "ToMove", typeof(VRCPositionConstraint), "GlobalWeight"), Is.EqualTo(1f));
        }
    
        [Test]
        public void MixedZeroDurationMoves_DoNotGenerateNonFiniteCurves()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var instantTarget = CreateChild(root, "InstantTarget");
            var smoothTarget = CreateChild(root, "SmoothTarget");
            AddMove(root, "Instant", toMove, instantTarget, position: true, rotation: false, scale: false);
            var smoothMove = AddMove(root, "Smooth", toMove, smoothTarget, position: true, rotation: false, scale: false);
            smoothMove.TransitionTime = 1f;
    
            AvatarProcessor.ProcessAvatar(root);
    
            foreach (var clip in GeneratedClips(root))
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                foreach (var key in curve.keys)
                {
                    Assert.That(float.IsNaN(key.value), Is.False,
                        $"{clip.name} generated NaN for {binding.propertyName}");
                }
    
                if (!binding.propertyName.Contains("Weight") || curve.length <= 1)
                    continue;
    
                foreach (var key in curve.keys)
                {
                    Assert.That(float.IsInfinity(key.inTangent), Is.False,
                        $"{clip.name} generated a non-finite in tangent for {binding.propertyName}");
                    Assert.That(float.IsInfinity(key.outTangent), Is.False,
                        $"{clip.name} generated a non-finite out tangent for {binding.propertyName}");
                }
            }
        }
    
        [Test]
        public void TimedFixedWorldMove_DoesNotBindANegativeSourceWeight()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var target = CreateChild(root, "Target");
            var move = AddMove(root, "Rule", toMove, target, position: true, rotation: false, scale: false);
            move.FixToWorld = true;
            move.TransitionTime = 1f;
    
            AvatarProcessor.ProcessAvatar(root);
    
            var bindings = GeneratedClips(root)
                .SelectMany(AnimationUtility.GetCurveBindings)
                .Where(binding => binding.path == "ToMove" && binding.type == typeof(VRCPositionConstraint))
                .Select(binding => binding.propertyName);
            Assert.That(bindings, Has.None.Contains("source-1"));
        }
    
        private ModularAvatarReactiveMove AddMove(
            GameObject root,
            string name,
            GameObject toMove,
            GameObject whereTo,
            bool position,
            bool rotation,
            bool scale
        )
        {
            var controller = CreateChild(root, name);
            var menu = controller.AddComponent<ModularAvatarMenuItem>();
            menu.automaticValue = false;
            menu.isDefault = false;
            menu.Control = new VRCExpressionsMenu.Control
            {
                name = name,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                value = 1,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = name }
            };
    
            var move = controller.AddComponent<ModularAvatarReactiveMove>();
            move.ToMove = new AvatarObjectReference(toMove);
            move.WhereTo = new AvatarObjectReference(whereTo);
            move.SetsPosition = position;
            move.SetsRotation = rotation;
            move.SetsScale = scale;
            return move;
        }
    
        [Test]
        public void DeltaTime_DeepInternalChain_SuppressesInitialTransitionOnly()
        {
            var root = CreateRoot("DeltaTimeRoot");
            var target = CreateChild(root, "Target");
            var destination = CreateChild(root, "Destination");
            var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            var graph = new ReactionGraph();
            graph.Parameters.EnsureParameter("input", 0);
    
            for (var i = 0; i < 12; i++)
            {
                graph.Parameters.EnsureParameter($"internal-{i}", 0);
                graph.AddNode(new ReactionNode(
                    i == 0
                        ? new ParameterExpression("input", 0.5f, ParameterExpression.ConditionMode.GreaterThan)
                        : new InternalParameterCondition($"internal-{i - 1}"),
                    new DriveInternalParameter($"internal-{i}", true)
                ));
            }
    
            graph.AddNode(new ReactionNode(
                new InternalParameterCondition("internal-11"),
                new ReactiveMoveAction(target.transform, destination.transform, false,
                    ReactiveMoveChannels.Position, 2f)
            ));
    
            var context = new nadena.dev.ndmf.BuildContext(root, null,
                PlatformRegistry.PlatformProviders[WellKnownPlatforms.VRChatAvatar30], true);
            var services = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            
            // Alias the FX controller onto an animator to make it easy to test in context.
            var controller = VirtualAnimatorController.Create(services.ControllerContext.CloneContext);
            services.ControllerContext.Controllers[FX] = controller;
            services.ControllerContext.Controllers[animator] = controller;
            
            var injectRO = context.ActivateExtensionContextRecursive<InjectROBaseLayer>();
    
            var backend = new VRChatBlendTreeBackend(controller, new UnityBlendTreeBackend(context, controller));
            backend.PreprocessGraph(graph);
            ILBuild.Simplify(graph);
            backend.Build(graph);
            
            // Since we're manually building things we also have to manually set up the base layer
            var baseState = injectRO.Layer!.StateMachine!.AddState("Base");
            baseState.Motion = backend.BaseBlendTree;
            injectRO.Layer!.StateMachine.DefaultState = baseState;
            
            context.DeactivateAllExtensionContexts();
            animator.Rebind();
            var constraint = target.GetComponent<VRCPositionConstraint>();
    
            animator.SetFloat("__MA/SyncInit", 1);
            animator.SetFloat("input", 1);
            for (var i = 0; i < 30; i++) animator.Update(1f / 60f);
            Assert.That(constraint.Sources[1].Weight, Is.EqualTo(1f).Within(0.001f));
    
            animator.SetFloat("input", 0);
            for (var i = 0; i < 30; i++) animator.Update(1f / 60f);
            Assert.That(constraint.GlobalWeight, Is.Zero.Within(0.001f));
    
            animator.SetFloat("input", 1);
            for (var i = 0; i < 30; i++) animator.Update(1f / 60f);
            Assert.That(constraint.Sources[1].Weight, Is.GreaterThan(0.001f).And.LessThan(0.999f));
        }
    
        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
        }
    
        private static void AssertConstraintState(VRCConstraintBase constraint)
        {
            Assert.That(constraint.IsActive, Is.True);
            Assert.That(constraint.Locked, Is.True);
            Assert.That(constraint.SolveInLocalSpace, Is.False);
            Assert.That(constraint.GlobalWeight, Is.Zero);
            Assert.That(constraint.Sources.Select(source => source.Weight), Is.All.EqualTo(0f));
        }
    
        private static void AssertSources(VRCConstraintBase constraint, params Transform[] expected)
        {
            CollectionAssert.AreEqual(expected, constraint.Sources.Select(source => source.SourceTransform));
            foreach (var source in constraint.Sources)
            {
                Assert.That(source.ParentPositionOffset, Is.EqualTo(Vector3.zero));
                Assert.That(source.ParentRotationOffset, Is.EqualTo(Vector3.zero));
                Assert.That(source.Weight, Is.Zero);
            }
        }
    
        private static IEnumerable<AnimationClip> GeneratedClips(GameObject root)
        {
            var controller = (AnimatorController)FindFxController(root).animatorController;
            return controller.layers
                .SelectMany(layer => CollectClips(layer.stateMachine.defaultState.motion))
                .Distinct();
        }
    
        private static IEnumerable<AnimationClip> CollectClips(Motion motion)
        {
            if (motion is AnimationClip clip)
            {
                yield return clip;
                yield break;
            }
    
            if (motion is BlendTree tree)
                foreach (var child in tree.children)
                foreach (var childClip in CollectClips(child.motion))
                    yield return childClip;
        }
    
        private static float CurveValue(AnimationClip clip, string path, System.Type type, string property)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property));
            Assert.That(curve, Is.Not.Null, $"missing {type.Name}.{property} curve");
            return curve.keys.Single(key => key.time == 0).value;
        }
    
        private static bool HasCurveValue(AnimationClip clip, string path, System.Type type, string property, float value)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property));
            return curve != null && curve.keys.Any(key => key.time == 0 && key.value == value);
        }
    
        private static string SourceWeightProperty(int sourceIndex)
        {
            return sourceIndex < 16
                ? $"Sources.source{sourceIndex}.Weight"
                : $"Sources.overflowList.Array.data[{sourceIndex - 16}].Weight";
        }
    }
}

#endif
