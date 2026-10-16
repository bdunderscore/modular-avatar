using System.Collections;
using System.Diagnostics.CodeAnalysis;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace nadena.dev.modular_avatar.test._PlayModeTests.ReactiveComponents
{
    [SuppressMessage("ReSharper", "Unity.PreferAddressByIdToGraphicsParams")]
    public class ReactiveMoveTests : TestBase
    {
        private const float PositionTolerance = 0.001f;
        private const float RotationTolerance = 0.01f;
        private const float ScaleTolerance = 0.001f;
    
        [UnityTest]
        public IEnumerator PositionAndRotationConstraintsFollowWorldPoseAndRestoreBaseline()
        {
            var avatar = CreateRoot();
            var toMoveParent = CreatePoseParent(avatar, "ToMove Parent", new Vector3(3, -2, 1), new Vector3(15, 35, -10), Vector3.one * 1.5f);
            var toMove = CreateChild(toMoveParent, "To Move");
            SetLocalPose(toMove.transform, new Vector3(0.4f, -0.2f, 0.8f), new Vector3(8, -16, 27), new Vector3(0.7f, 1.1f, 1.3f));
            var targetParent = CreatePoseParent(avatar, "Target Parent", new Vector3(-4, 3, 2), new Vector3(-20, 55, 12), Vector3.one * 0.75f);
            var target = CreateChild(targetParent, "Target");
            SetLocalPose(target.transform, new Vector3(-0.6f, 1.2f, 0.3f), new Vector3(25, 10, -35), Vector3.one);
            var baseline = CapturePose(toMove.transform);
    
            AddMove(avatar, "move", "move", toMove, target, true, true, false);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            SetParam(animator, "move", 1);
            yield return null;
            yield return null;
    
            AssertPositionAndRotationMatch(toMove.transform, target.transform);
            AssertScaleMatches(toMove.transform, baseline.Scale);
    
            SetParam(animator, "move", 0);
            yield return null;
            yield return null;
    
            AssertPoseMatches(toMove.transform, baseline);
            Assert.That(toMove.GetComponent<VRCPositionConstraint>().GlobalWeight, Is.Zero);
            Assert.That(toMove.GetComponent<VRCRotationConstraint>().GlobalWeight, Is.Zero);
        }
    
        [UnityTest]
        public IEnumerator SplitConstraintsFollowIndependentPositionAndRotationTargets()
        {
            var avatar = CreateRoot();
            var toMoveParent = CreatePoseParent(avatar, "ToMove Parent", new Vector3(2, 1, -3), new Vector3(10, 25, 15), Vector3.one * 1.25f);
            var toMove = CreateChild(toMoveParent, "To Move");
            SetLocalPose(toMove.transform, new Vector3(0.2f, 0.5f, -0.4f), new Vector3(-5, 15, 30), new Vector3(0.8f, 1.2f, 0.9f));
            var baseline = CapturePose(toMove.transform);
    
            var positionParent = CreatePoseParent(avatar, "Position Parent", new Vector3(-3, 4, 1), new Vector3(20, -40, 5), Vector3.one);
            var positionTarget = CreateChild(positionParent, "Position Target");
            SetLocalPose(positionTarget.transform, new Vector3(0.7f, -0.5f, 1.1f), new Vector3(0, 0, 0), Vector3.one);
            var rotationParent = CreatePoseParent(avatar, "Rotation Parent", new Vector3(5, -2, 4), new Vector3(-15, 45, 20), Vector3.one);
            var rotationTarget = CreateChild(rotationParent, "Rotation Target");
            SetLocalPose(rotationTarget.transform, new Vector3(0, 0, 0), new Vector3(35, -20, 65), Vector3.one);
    
            AddMove(avatar, "position move", "position", toMove, positionTarget, true, false, false);
            AddMove(avatar, "rotation move", "rotation", toMove, rotationTarget, false, true, false);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            var positionConstraint = toMove.GetComponent<VRCPositionConstraint>();
            var rotationConstraint = toMove.GetComponent<VRCRotationConstraint>();
            Assert.That(positionConstraint, Is.Not.Null);
            Assert.That(rotationConstraint, Is.Not.Null);
    
            SetParam(animator, "position", 1);
            SetParam(animator, "rotation", 1);
            yield return null;
            yield return null;
    
            AssertPositionMatches(toMove.transform, positionTarget.transform.position);
            AssertRotationMatches(toMove.transform, rotationTarget.transform.rotation);
            AssertScaleMatches(toMove.transform, baseline.Scale);
            Assert.That(positionConstraint.GlobalWeight, Is.EqualTo(1));
            Assert.That(rotationConstraint.GlobalWeight, Is.EqualTo(1));
            
            SetParam(animator, "position", 0);
            SetParam(animator, "rotation", 1);
            yield return null;
            yield return null;
            
            AssertPositionMatches(toMove.transform, baseline.Position);
            AssertRotationMatches(toMove.transform, rotationTarget.transform.rotation);
            AssertScaleMatches(toMove.transform, baseline.Scale);
            Assert.That(positionConstraint.GlobalWeight, Is.EqualTo(0));
            Assert.That(rotationConstraint.GlobalWeight, Is.EqualTo(1));
            
            SetParam(animator, "position", 1);
            SetParam(animator, "rotation", 0);
            yield return null;
            yield return null;
            
            AssertPositionMatches(toMove.transform, positionTarget.transform.position);
            AssertRotationMatches(toMove.transform, baseline.Rotation);
            AssertScaleMatches(toMove.transform, baseline.Scale);
            Assert.That(positionConstraint.GlobalWeight, Is.EqualTo(1));
            Assert.That(rotationConstraint.GlobalWeight, Is.EqualTo(0));
    
            SetParam(animator, "position", 0);
            SetParam(animator, "rotation", 0);
            yield return null;
            yield return null;
    
            AssertPoseMatches(toMove.transform, baseline);
            Assert.That(positionConstraint.GlobalWeight, Is.Zero);
            Assert.That(rotationConstraint.GlobalWeight, Is.Zero);
        }
    
        [UnityTest]
        public IEnumerator ScaleConstraintFollowsLossyScaleWithoutChangingPositionOrRotation()
        {
            var avatar = CreateRoot();
            var toMoveParent = CreatePoseParent(avatar, "ToMove Parent", Vector3.zero, Vector3.zero, new Vector3(1.5f, 2.5f, 0.5f));
            var toMove = CreateChild(toMoveParent, "To Move");
            SetLocalPose(toMove.transform, new Vector3(0.4f, -0.6f, 1.1f), Vector3.zero, new Vector3(0.8f, 1.1f, 1.4f));
            var baseline = CapturePose(toMove.transform);
            var targetParent = CreatePoseParent(avatar, "Target Parent", Vector3.zero, Vector3.zero, new Vector3(2f, 3f, 4f));
            var target = CreateChild(targetParent, "Target");
            SetLocalPose(target.transform, Vector3.zero, Vector3.zero, new Vector3(1.2f, 0.7f, 0.8f));
    
            AddMove(avatar, "scale move", "scale", toMove, target, false, false, true);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            SetParam(animator, "scale", 1);
            yield return null;
            yield return null;
    
            AssertScaleMatches(toMove.transform, target.transform.lossyScale);
            AssertPositionMatches(toMove.transform, baseline.Position);
            AssertRotationMatches(toMove.transform, baseline.Rotation);
    
            SetParam(animator, "scale", 0);
            yield return null;
            yield return null;
    
            AssertPoseMatches(toMove.transform, baseline);
            Assert.That(toMove.GetComponent<VRCScaleConstraint>().GlobalWeight, Is.Zero);
        }
    
        [UnityTest]
        public IEnumerator LaterActiveMoveWinsAndDeactivationRestoresAuthoredPose()
        {
            var avatar = CreateRoot();
            var toMoveParent = CreatePoseParent(avatar, "ToMove Parent", new Vector3(1, -3, 2), new Vector3(5, 25, -30), Vector3.one);
            var toMove = CreateChild(toMoveParent, "To Move");
            SetLocalPose(toMove.transform, new Vector3(0.3f, 0.6f, -0.9f), new Vector3(15, -10, 20), Vector3.one);
            var baseline = CapturePose(toMove.transform);
            var firstTarget = CreateTarget(avatar, "First Target", new Vector3(-2, 4, 3), new Vector3(25, 30, -15));
            var laterTarget = CreateTarget(avatar, "Later Target", new Vector3(5, -1, -4), new Vector3(-20, 60, 35));
    
            AddMove(avatar, "first move", "first", toMove, firstTarget, true, true, false);
            AddMove(avatar, "later move", "later", toMove, laterTarget, true, true, false);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            SetParam(animator, "first", 1);
            SetParam(animator, "later", 1);
            yield return null;
            yield return null;
    
            AssertPositionAndRotationMatch(toMove.transform, laterTarget.transform);
    
            SetParam(animator, "first", 0);
            SetParam(animator, "later", 0);
            yield return null;
            yield return null;
    
            AssertPoseMatches(toMove.transform, baseline);
            Assert.That(toMove.GetComponent<VRCPositionConstraint>().GlobalWeight, Is.Zero);
            Assert.That(toMove.GetComponent<VRCRotationConstraint>().GlobalWeight, Is.Zero);
        }
    
        [UnityTest]
        public IEnumerator TimedRotation_DoesNotDelayIndependentPosition()
        {
            var avatar = CreateRoot();
            var toMove = CreateChild(avatar, "To Move");
            var positionTarget = CreateTarget(avatar, "Position Target", new Vector3(2, 3, 4), Vector3.zero);
            var rotationTarget = CreateTarget(avatar, "Rotation Target", Vector3.zero, new Vector3(15, 30, 45));
    
            AddMove(avatar, "position move", "position", toMove, positionTarget, true, false, false);
            var rotationMove = AddMove(
                avatar, "rotation move", "rotation", toMove, rotationTarget, false, true, false);
            rotationMove.TransitionTime = 1f;
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            var positionConstraint = toMove.GetComponent<VRCPositionConstraint>();
            var rotationConstraint = toMove.GetComponent<VRCRotationConstraint>();
            Assert.That(positionConstraint.Sources, Has.Count.EqualTo(1));
            Assert.That(rotationConstraint.Sources, Has.Count.EqualTo(2));
    
            SetParam(animator, "position", 1);
            SetParam(animator, "rotation", 1);
            yield return null;
            yield return null;
    
            AssertPositionMatches(toMove.transform, positionTarget.transform.position);
            Assert.That(positionConstraint.GlobalWeight, Is.EqualTo(1));
        }
    
        [UnityTest]
        public IEnumerator TransitionedMove_ActivatesAndReleasesFromDefaultOff()
        {
            var avatar = CreateRoot();
            var toMove = CreateChild(avatar, "To Move");
            var target = CreateTarget(avatar, "Target", new Vector3(3, 2, 1), Vector3.zero);
            var baseline = CapturePose(toMove.transform);
            AddMove(avatar, "move", "move", toMove, target, true, false, false, transitionTime: 1f);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            AssertPoseMatches(toMove.transform, baseline);
            SetParam(animator, "move", 1);
            yield return WaitForReactionDispatch();
            yield return new WaitForSeconds(1.2f);
            AssertPositionMatches(toMove.transform, target.transform.position);
    
            SetParam(animator, "move", 0);
            yield return WaitForReactionDispatch();
            AssertPoseMatches(toMove.transform, baseline);
            Assert.That(toMove.GetComponent<VRCPositionConstraint>().GlobalWeight, Is.Zero);
        }
    
        [UnityTest]
        public IEnumerator MissingDestinationMove_Position_ImmediateRelease()
        {
            return RunMissingDestinationMove(true, false, false, 0);
        }
    
        [UnityTest]
        public IEnumerator MissingDestinationMove_Rotation_ImmediateRelease()
        {
            return RunMissingDestinationMove(false, true, false, 0);
        }
    
        [UnityTest]
        public IEnumerator MissingDestinationMove_Scale_ImmediateRelease()
        {
            return RunMissingDestinationMove(false, false, true, 0);
        }
    
        [UnityTest]
        public IEnumerator MissingDestinationMove_Position_TransitionedRestore()
        {
            return RunMissingDestinationMove(true, false, false, 0.5f);
        }
    
        [UnityTest]
        public IEnumerator MissingDestinationMove_Rotation_TransitionedRestore()
        {
            return RunMissingDestinationMove(false, true, false, 0.5f);
        }
    
        [UnityTest]
        public IEnumerator MissingDestinationMove_Scale_TransitionedRestore()
        {
            return RunMissingDestinationMove(false, false, true, 0.5f);
        }
    
        private IEnumerator RunMissingDestinationMove(bool disablePosition, bool disableRotation, bool disableScale,
            float transitionTime)
        {
            var avatar = CreateRoot();
            var toMove = CreateChild(avatar, "To Move");
            SetLocalPose(toMove.transform, new Vector3(1, -2, 3), new Vector3(10, -20, 30),
                new Vector3(0.8f, 1.1f, 1.3f));
            var baseline = CapturePose(toMove.transform);
            var target = CreateChild(avatar, "Target");
            SetLocalPose(target.transform, new Vector3(-4, 5, -6), new Vector3(-25, 40, 15),
                new Vector3(1.4f, 0.7f, 1.6f));
    
            AddMove(avatar, "valid move", "valid", toMove, target, true, true, true,
                transitionTime: transitionTime);
            AddMove(avatar, "missing move", "missing", toMove, null, disablePosition, disableRotation,
                disableScale);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            var positionConstraint = toMove.GetComponent<VRCPositionConstraint>();
            var rotationConstraint = toMove.GetComponent<VRCRotationConstraint>();
            var scaleConstraint = toMove.GetComponent<VRCScaleConstraint>();
            Assert.That(positionConstraint, Is.Not.Null);
            Assert.That(rotationConstraint, Is.Not.Null);
            Assert.That(scaleConstraint, Is.Not.Null);
    
            if (transitionTime > 0)
            {
                SetParam(animator, "__MA/SyncInit", 1);
                yield return WaitForReactionDispatch();
            }
            SetParam(animator, "valid", 1);
            yield return WaitForReactionDispatch();
            if (transitionTime > 0)
                yield return new WaitForSeconds(transitionTime + 0.1f);
    
            AssertPoseMatches(toMove.transform, CapturePose(target.transform));
            AssertConstraintIsWeighted(positionConstraint);
            AssertConstraintIsWeighted(rotationConstraint);
            AssertConstraintIsWeighted(scaleConstraint);
    
            SetParam(animator, "missing", 1);
            yield return WaitForReactionDispatch();
    
            AssertSelectedConstraintIsDisabled(positionConstraint, rotationConstraint, scaleConstraint,
                disablePosition, disableRotation, disableScale);
            AssertSelectedChannelMatches(toMove.transform, baseline, disablePosition, disableRotation, disableScale);
            AssertOtherChannelsMatch(toMove.transform, target.transform, disablePosition, disableRotation, disableScale);
            AssertOtherConstraintsAreWeighted(positionConstraint, rotationConstraint, scaleConstraint,
                disablePosition, disableRotation, disableScale);
    
            SetParam(animator, "missing", 0);
            yield return WaitForReactionDispatch();
            if (transitionTime > 0)
            {
                yield return new WaitForSeconds(0.1f);
                AssertSelectedChannelIsBetween(toMove.transform, baseline, target.transform, disablePosition,
                    disableRotation, disableScale);
                yield return new WaitForSeconds(transitionTime);
            }
    
            AssertPoseMatches(toMove.transform, CapturePose(target.transform));
            AssertConstraintIsWeighted(positionConstraint);
            AssertConstraintIsWeighted(rotationConstraint);
            AssertConstraintIsWeighted(scaleConstraint);
        }
    
        [UnityTest]
        public IEnumerator FixedWorldMove_HoldsWorldPositionWhenParentMoves()
        {
            var avatar = CreateRoot();
            var parent = CreatePoseParent(avatar, "Parent", new Vector3(1, 2, 3), Vector3.zero, Vector3.one);
            var toMove = CreateChild(parent, "To Move");
            toMove.transform.localPosition = new Vector3(2, 0, 0);
            var target = CreateTarget(avatar, "Unused Target", Vector3.zero, Vector3.zero);
            AddMove(avatar, "move", "move", toMove, target, true, false, false, fixToWorld: true);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            yield return null;
    
            var fixedPosition = toMove.transform.position;
            SetParam(animator, "move", 1);
            yield return WaitForReactionDispatch();
            parent.transform.position += new Vector3(4, -3, 2);
            yield return WaitForReactionDispatch();
    
            AssertPositionMatches(toMove.transform, fixedPosition);
            Assert.That(toMove.GetComponent<VRCPositionConstraint>().GlobalWeight, Is.EqualTo(1));
            Assert.That(toMove.GetComponent<VRCPositionConstraint>().FreezeToWorld, Is.True);
        }
    
    
    
        [UnityTest]
        public IEnumerator TimedPositionAndRotation_UseIndependentTransitionGroups()
        {
            var avatar = CreateRoot();
            var toMove = CreateChild(avatar, "To Move");
            var baseline = CapturePose(toMove.transform);
            var positionTarget = CreateTarget(avatar, "Position Target", new Vector3(2, 3, 4), Vector3.zero);
            var rotationTarget = CreateTarget(avatar, "Rotation Target", Vector3.zero, new Vector3(15, 30, 45));
            AddMove(avatar, "position move", "position", toMove, positionTarget, true, false, false,
                transitionTime: 2f);
            AddMove(avatar, "rotation move", "rotation", toMove, rotationTarget, false, true, false,
                transitionTime: 2f);
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            SetParam(animator, "__MA/SyncInit", 1);
            yield return WaitForReactionDispatch();
    
            SetParam(animator, "position", 1);
            yield return WaitForReactionDispatch();
            yield return new WaitForSeconds(0.25f);
            AssertPositionIsBetween(toMove.transform, baseline.Position, positionTarget.transform.position);
            AssertRotationMatches(toMove.transform, baseline.Rotation);
    
            SetParam(animator, "rotation", 1);
            yield return WaitForReactionDispatch();
            yield return new WaitForSeconds(0.25f);
            AssertPositionIsBetween(toMove.transform, baseline.Position, positionTarget.transform.position);
            AssertRotationIsBetween(toMove.transform, baseline.Rotation, rotationTarget.transform.rotation);
    
            yield return new WaitForSeconds(2.1f);
            AssertPositionMatches(toMove.transform, positionTarget.transform.position);
            AssertRotationMatches(toMove.transform, rotationTarget.transform.rotation);
        }
    
        private GameObject CreatePoseParent(GameObject avatar, string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            var parent = CreateChild(avatar, name);
            SetLocalPose(parent.transform, position, rotation, scale);
            return parent;
        }
    
        private GameObject CreateTarget(GameObject avatar, string name, Vector3 position, Vector3 rotation)
        {
            var target = CreateChild(avatar, name);
            SetLocalPose(target.transform, position, rotation, Vector3.one);
            return target;
        }
    
        private ModularAvatarReactiveMove AddMove(GameObject avatar, string name, string parameter, GameObject toMove,
            GameObject target, bool setPosition, bool setRotation, bool setScale, bool fixToWorld = false,
            float transitionTime = 0)
        {
            var controller = CreateChild(avatar, name);
            controller.AddComponent<ModularAvatarMenuItem>().PortableControl.Parameter = parameter;
            var move = controller.AddComponent<ModularAvatarReactiveMove>();
            move.ToMove = new AvatarObjectReference(toMove);
            move.WhereTo = new AvatarObjectReference(target);
            move.SetsPosition = setPosition;
            move.SetsRotation = setRotation;
            move.SetsScale = setScale;
            move.FixToWorld = fixToWorld;
            move.TransitionTime = transitionTime;
            return move;
        }
    
        // DeltaTimeProcessor has five dispatch stages; allow five additional frames for the
        // reaction branch to settle before making an observable-state assertion.
        private static IEnumerator WaitForReactionDispatch()
        {
            for (var frame = 0; frame < 10; frame++)
                yield return null;
        }
    
        private static void AssertSelectedChannelMatches(Transform transform, Pose baseline, bool disablePosition,
            bool disableRotation, bool disableScale)
        {
            if (disablePosition) AssertPositionMatches(transform, baseline.Position);
            if (disableRotation) AssertRotationMatches(transform, baseline.Rotation);
            if (disableScale) AssertScaleMatches(transform, baseline.Scale);
        }
    
        private static void AssertOtherChannelsMatch(Transform transform, Transform target, bool disablePosition,
            bool disableRotation, bool disableScale)
        {
            if (!disablePosition) AssertPositionMatches(transform, target.position);
            if (!disableRotation) AssertRotationMatches(transform, target.rotation);
            if (!disableScale) AssertScaleMatches(transform, target.lossyScale);
        }
    
        private static void AssertSelectedConstraintIsDisabled(VRCPositionConstraint positionConstraint,
            VRCRotationConstraint rotationConstraint, VRCScaleConstraint scaleConstraint, bool disablePosition,
            bool disableRotation, bool disableScale)
        {
            var constraint = SelectConstraint(positionConstraint, rotationConstraint, scaleConstraint, disablePosition,
                disableRotation, disableScale);
            Assert.That(constraint.GlobalWeight, Is.Zero.Within(0.001f));
        }
    
        private static void AssertOtherConstraintsAreWeighted(VRCPositionConstraint positionConstraint,
            VRCRotationConstraint rotationConstraint, VRCScaleConstraint scaleConstraint, bool disablePosition,
            bool disableRotation, bool disableScale)
        {
            if (!disablePosition) AssertConstraintIsWeighted(positionConstraint);
            if (!disableRotation) AssertConstraintIsWeighted(rotationConstraint);
            if (!disableScale) AssertConstraintIsWeighted(scaleConstraint);
        }
    
        private static VRCConstraintBase SelectConstraint(VRCPositionConstraint positionConstraint,
            VRCRotationConstraint rotationConstraint, VRCScaleConstraint scaleConstraint, bool disablePosition,
            bool disableRotation, bool disableScale)
        {
            Assert.That(disablePosition || disableRotation || disableScale, Is.True);
            Assert.That((disablePosition ? 1 : 0) + (disableRotation ? 1 : 0) + (disableScale ? 1 : 0),
                Is.EqualTo(1));
            if (disablePosition) return positionConstraint;
            if (disableRotation) return rotationConstraint;
            return scaleConstraint;
        }
    
        private static void AssertConstraintIsWeighted(VRCConstraintBase constraint)
        {
            Assert.That(constraint.GlobalWeight, Is.EqualTo(1f).Within(0.001f));
            Assert.That(constraint.Sources, Is.Not.Empty);
            var hasWeightedSource = false;
            foreach (var source in constraint.Sources)
                hasWeightedSource |= source.Weight > 0.999f;
            Assert.That(hasWeightedSource, Is.True);
        }
    
        private static void AssertSelectedChannelIsBetween(Transform transform, Pose baseline, Transform target,
            bool disablePosition, bool disableRotation, bool disableScale)
        {
            if (disablePosition) AssertPositionIsBetween(transform, baseline.Position, target.position);
            if (disableRotation) AssertRotationIsBetween(transform, baseline.Rotation, target.rotation);
            if (disableScale) AssertScaleIsBetween(transform, baseline.Scale, target.lossyScale);
        }
    
        private static Pose CapturePose(Transform transform)
        {
            return new Pose(transform.position, transform.rotation, transform.lossyScale);
        }
    
        private static void SetLocalPose(Transform transform, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(rotation);
            transform.localScale = scale;
        }
    
        private static void AssertPoseMatches(Transform transform, Pose expected)
        {
            AssertPositionMatches(transform, expected.Position);
            AssertRotationMatches(transform, expected.Rotation);
            AssertScaleMatches(transform, expected.Scale);
        }
    
        private static void AssertPositionAndRotationMatch(Transform transform, Transform target)
        {
            AssertPositionMatches(transform, target.position);
            AssertRotationMatches(transform, target.rotation);
        }
    
        private static void AssertPositionMatches(Transform transform, Vector3 expected)
        {
            Assert.LessOrEqual(
                Vector3.Distance(transform.position, expected),
                PositionTolerance,
                $"Expected position {expected}, but was {transform.position}"
            );
        }
    
        private static void AssertRotationMatches(Transform transform, Quaternion expected)
        {
            Assert.LessOrEqual(Quaternion.Angle(transform.rotation, expected), RotationTolerance);
        }
    
        private static void AssertPositionIsBetween(Transform transform, Vector3 baseline, Vector3 target)
        {
            var distanceFromBaseline = Vector3.Distance(transform.position, baseline);
            var distanceFromTarget = Vector3.Distance(transform.position, target);
            Assert.That(distanceFromBaseline, Is.GreaterThan(PositionTolerance));
            Assert.That(distanceFromTarget, Is.GreaterThan(PositionTolerance));
        }
    
        private static void AssertRotationIsBetween(Transform transform, Quaternion baseline, Quaternion target)
        {
            Assert.That(Quaternion.Angle(transform.rotation, baseline), Is.GreaterThan(RotationTolerance));
            Assert.That(Quaternion.Angle(transform.rotation, target), Is.GreaterThan(RotationTolerance));
        }
    
        private static void AssertScaleIsBetween(Transform transform, Vector3 baseline, Vector3 target)
        {
            var distanceFromBaseline = Vector3.Distance(transform.lossyScale, baseline);
            var distanceFromTarget = Vector3.Distance(transform.lossyScale, target);
            Assert.That(distanceFromBaseline, Is.GreaterThan(ScaleTolerance));
            Assert.That(distanceFromTarget, Is.GreaterThan(ScaleTolerance));
        }
    
        private static void AssertScaleMatches(Transform transform, Vector3 expected)
        {
            Assert.LessOrEqual(
                Vector3.Distance(transform.lossyScale, expected),
                ScaleTolerance,
                $"Expected lossy scale {expected}, but was {transform.lossyScale}"
            );
        }
    
        private readonly struct Pose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
    
            public Pose(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }
    }
}
