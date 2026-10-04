#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace UnitTests.ReactiveComponent
{
    internal class ReactiveMoveTests : TestBase
    {
        [Test]
        public void PositionAndRotationRules_CreateParentConstraint()
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

            Assert.That(toMove.GetComponents<VRCPositionConstraint>(), Is.Empty);
            Assert.That(toMove.GetComponents<VRCRotationConstraint>(), Is.Empty);
            var parent = toMove.GetComponents<VRCParentConstraint>().Single();
            AssertConstraintState(parent);
            AssertVector3(parent.PositionAtRest, new Vector3(1, 2, 3));
            AssertVector3(parent.RotationAtRest, new Vector3(10, 20, 30));
            AssertSources(parent, firstTarget.transform, secondTarget.transform);
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
        public void LaterMoveWinnerClips_OverrideEarlierMovePerTransformProperty()
        {
            var root = CreateRoot("root");
            var toMove = CreateChild(root, "ToMove");
            var earlierTarget = CreateChild(root, "EarlierTarget");
            var laterTarget = CreateChild(root, "LaterTarget");

            AddMove(root, "EarlierRule", toMove, earlierTarget, position: true, rotation: true, scale: true);
            AddMove(root, "LaterRule", toMove, laterTarget, position: true, rotation: true, scale: true);

            AvatarProcessor.ProcessAvatar(root);

            var constraints = new VRCConstraintBase[]
            {
                toMove.GetComponents<VRCParentConstraint>().Single(),
                toMove.GetComponents<VRCScaleConstraint>().Single()
            };
            var clips = GeneratedClips(root).ToList();
            const string path = "ToMove";

            foreach (var constraint in constraints)
            {
                var constraintType = constraint.GetType();
                var laterClip = clips.Single(clip =>
                    HasCurveValue(clip, path, constraintType, SourceWeightProperty(1), 1f));

                Assert.That(CurveValue(laterClip, path, constraintType, SourceWeightProperty(0)), Is.EqualTo(0f));
                Assert.That(CurveValue(laterClip, path, constraintType, "GlobalWeight"), Is.EqualTo(1f));
                AssertSources(constraint, earlierTarget.transform, laterTarget.transform);
            }
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
                .Where(layer => layer.name == VRChatBlendTreeBackend.ApplyLayerName)
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
