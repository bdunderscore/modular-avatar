using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.Simulator;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.test.Tests
{
    public class ReactiveMovePreviewTests : TestBase
    {
        [TestCase(WorldChannel.Position)]
        [TestCase(WorldChannel.Rotation)]
        [TestCase(WorldChannel.Scale)]
        public void FixedChildChannelRestoresCurrentSourceValueAfterParentPreviewMove(WorldChannel channel)
        {
            var root = CreateRoot("root");
            root.transform.rotation = Quaternion.Euler(17, -31, 9);
            root.transform.localScale = new Vector3(1.3f, 0.8f, 1.7f);
            var parent = CreateChild(root, "parent");
            parent.transform.localPosition = new Vector3(2, -1, 4);
            parent.transform.localRotation = Quaternion.Euler(-12, 27, 43);
            parent.transform.localScale = new Vector3(0.6f, 1.4f, 0.9f);
            var child = CreateChild(parent, "child");
            child.transform.localPosition = new Vector3(-3, 5, 1);
            child.transform.localRotation = Quaternion.Euler(33, -18, 24);
            child.transform.localScale = new Vector3(1.2f, 0.7f, 1.6f);
            var target = CreateChild(root, "target");
            target.transform.position = new Vector3(9, -6, 11);
            target.transform.rotation = Quaternion.Euler(61, 14, -38);
            target.transform.localScale = new Vector3(2.1f, 0.5f, 1.8f);
            var staleDestination = TrackObject(new GameObject("stale destination"));
            var childBone = CreateChild(child, "child bone");
            childBone.transform.localPosition = new Vector3(0, 1, 0);
    
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { childBone.transform };
            var proxy = TrackObject(new GameObject("proxy")).AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { childBone.transform };
    
            AddMove(root, child, staleDestination, fixToWorld: true,
                position: channel == WorldChannel.Position, rotation: channel == WorldChannel.Rotation,
                scale: channel == WorldChannel.Scale);
            AddMove(root, parent, target, fixToWorld: false, position: true, rotation: true, scale: true);
            AddMove(root, child, target, fixToWorld: false,
                position: channel != WorldChannel.Position, rotation: channel != WorldChannel.Rotation,
                scale: channel != WorldChannel.Scale);
    
            var handle = new ShadowHandle(new Dictionary<Renderer, Renderer> { { renderer, proxy } });
            var previewContext = new PreviewContext { ShadowBoneManager = handle };
            var context = new ComputeContext("fixed child reactive move preview");
            try
            {
                using (previewContext.Activate())
                {
                    var node = new ReactiveMovePreview().Instantiate(
                        RenderGroup.For(renderer).WithData(root),
                        new[] { ((Renderer)renderer, (Renderer)proxy) },
                        context
                    ).Result;
    
                    handle.SyncPoses();
                    node.OnFrameGroup();
    
                    var parentShadow = handle.GetExistingShadow(parent.transform);
                    var childShadow = handle.GetExistingShadow(child.transform);
                    AssertWorldChannelEquals(childShadow, child.transform, channel,
                        "The fixed child channel must retain its authored world value.");
                    AssertWorldChannelEquals(parentShadow, target.transform, channel,
                        "The normal parent preview move must still change the parent.");
                    foreach (WorldChannel otherChannel in Enum.GetValues(typeof(WorldChannel)))
                    {
                        if (otherChannel == channel) continue;
                        AssertWorldChannelEquals(childShadow, target.transform, otherChannel,
                            "Other child channels must continue matching their preview target.");
                    }
                    Assert.That(handle.HasShadow(staleDestination.transform), Is.False,
                        "Fixed directives must not use their destination to restore an authored channel.");
                    var childBoneShadow = handle.GetExistingShadow(childBone.transform);
                    Assert.That(proxy.bones, Is.EqualTo(new[] { childBoneShadow }));
                    Assert.That(childBoneShadow.parent, Is.EqualTo(childShadow),
                        "A descendant shadow bone must remain attached to the moved shadow hierarchy.");
    
                    ChangeSourceChannel(child.transform, channel);
                    handle.SyncPoses();
                    node.OnFrameGroup();
    
                    AssertWorldChannelEquals(childShadow, child.transform, channel,
                        "The fixed child channel must use the source value from the current preview frame.");
                }
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
                handle.Dispose();
            }
        }
    
        [Test]
        public void NestedFixedDirectivesRestoreParentBeforeChildRegardlessOfComponentOrder()
        {
            var root = CreateRoot("root");
            var parent = CreateChild(root, "parent");
            parent.transform.position = new Vector3(1, 2, 3);
            var child = CreateChild(parent, "child");
            child.transform.localPosition = new Vector3(0, 4, 0);
            child.transform.localRotation = Quaternion.Euler(15, 25, 5);
            child.transform.localScale = new Vector3(2, 0.5f, 1.5f);
            var target = CreateChild(root, "target");
            target.transform.position = new Vector3(-8, 5, 12);
            target.transform.rotation = Quaternion.Euler(35, -21, 48);
            target.transform.localScale = new Vector3(1.6f, 0.7f, 1.3f);
            var staleDestination = TrackObject(new GameObject("stale destination"));
    
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { child.transform };
            var proxy = TrackObject(new GameObject("proxy")).AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { child.transform };
    
            // The child directive comes first. Restoring in action order would leave it displaced when its parent
            // is restored afterward.
            // Channels are also deliberately reversed; world scale must be restored after world rotation.
            AddMove(root, child, staleDestination, fixToWorld: true, position: false, rotation: false, scale: true);
            AddMove(root, child, staleDestination, fixToWorld: true, position: false, rotation: true, scale: false);
            AddMove(root, child, staleDestination, fixToWorld: true, position: true, rotation: false, scale: false);
            AddMove(root, parent, target, fixToWorld: false, position: true, rotation: true, scale: true);
            AddMove(root, parent, staleDestination, fixToWorld: true, position: true, rotation: false, scale: false);
    
            var handle = new ShadowHandle(new Dictionary<Renderer, Renderer> { { renderer, proxy } });
            var previewContext = new PreviewContext { ShadowBoneManager = handle };
            var context = new ComputeContext("nested fixed reactive move preview");
            try
            {
                using (previewContext.Activate())
                {
                    var node = new ReactiveMovePreview().Instantiate(
                        RenderGroup.For(renderer).WithData(root),
                        new[] { ((Renderer)renderer, (Renderer)proxy) },
                        context
                    ).Result;
    
                    handle.SyncPoses();
                    // An earlier preview stage has already displaced the incoming parent shadow.
                    handle.GetExistingShadow(parent.transform).position += new Vector3(5, -2, 3);
                    node.OnFrameGroup();
                }
    
                var parentShadow = handle.GetExistingShadow(parent.transform);
                var childShadow = handle.GetExistingShadow(child.transform);
                Assert.That(parentShadow.position, Is.EqualTo(parent.transform.position).Using(Vector3EqualityComparer.Instance));
                Assert.That(childShadow.position, Is.EqualTo(child.transform.position).Using(Vector3EqualityComparer.Instance));
                Assert.That(childShadow.rotation,
                    Is.EqualTo(child.transform.rotation).Using(QuaternionEqualityComparer.Instance));
                Assert.That(childShadow.lossyScale,
                    Is.EqualTo(child.transform.lossyScale).Using(Vector3EqualityComparer.Instance));
                Assert.That(parentShadow.rotation,
                    Is.EqualTo(target.transform.rotation).Using(QuaternionEqualityComparer.Instance),
                    "Unfixed parent rotation must continue to preview.");
                Assert.That(parentShadow.lossyScale,
                    Is.EqualTo(target.transform.lossyScale).Using(Vector3EqualityComparer.Instance),
                    "Unfixed parent scale must continue to preview.");
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
                handle.Dispose();
            }
        }
    
        [UnityTest]
        public IEnumerator FixedNullDestinationRestoresAfterItsControllerBecomesActive()
        {
            var root = CreateRoot("root");
            var parent = CreateChild(root, "parent");
            parent.transform.position = new Vector3(1, 2, 3);
            var moved = CreateChild(parent, "moved");
            moved.transform.localPosition = new Vector3(0, 2, 0);
            var target = CreateChild(root, "target");
            target.transform.position = new Vector3(-7, 9, 4);
            target.transform.rotation = Quaternion.Euler(40, 50, 60);
            target.transform.localScale = new Vector3(1.5f, 0.6f, 1.8f);
            var rotationTarget = CreateChild(root, "rotation target");
            rotationTarget.transform.rotation = Quaternion.Euler(-20, 67, 13);
            var renderer = moved.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { moved.transform };
            var proxy = TrackObject(new GameObject("proxy")).AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { moved.transform };
    
            var controller = CreateChild(root, "controller");
            controller.SetActive(false);
            AddMove(controller, moved, null, fixToWorld: true, position: true, rotation: false, scale: false);
            AddMove(root, parent, target, fixToWorld: false, position: true, rotation: true, scale: true);
            AddMove(root, moved, rotationTarget, fixToWorld: false, position: false, rotation: true, scale: false);
    
            var handle = new ShadowHandle(new Dictionary<Renderer, Renderer> { { renderer, proxy } });
            var previewContext = new PreviewContext { ShadowBoneManager = handle };
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            var inactiveContext = new ComputeContext("inactive fixed reactive move preview");
            var activeContext = new ComputeContext("active fixed reactive move preview");
            try
            {
                IRenderFilterNode inactiveNode;
                using (previewContext.Activate())
                {
                    inactiveNode = new ReactiveMovePreview().Instantiate(
                        RenderGroup.For(renderer).WithData(root),
                        new[] { ((Renderer)renderer, (Renderer)proxy) },
                        inactiveContext
                    ).Result;
                }
    
                ROSimulator.PropertyOverrides.Value = ROSimulator.PropertyOverrides.Value!.SetItem(
                    $"__ActiveSelfProxy/{controller.GetEntityId()}", 1.0f
                );
                for (var i = 0; i < 10 && !inactiveContext.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }
                Assert.That(inactiveContext.IsInvalidated, Is.True);
    
                using (previewContext.Activate())
                {
                    _ = inactiveNode.Refresh(
                        new[] { ((Renderer)renderer, (Renderer)proxy) }, activeContext, 0
                    ).Result;
                    handle.SyncPoses();
                    inactiveNode.OnFrameGroup();
                }
    
                var parentShadow = handle.GetExistingShadow(parent.transform);
                var movedShadow = handle.GetExistingShadow(moved.transform);
                Assert.That(parentShadow.position, Is.EqualTo(target.transform.position).Using(Vector3EqualityComparer.Instance));
                Assert.That(movedShadow.position, Is.EqualTo(moved.transform.position).Using(Vector3EqualityComparer.Instance),
                    "The active fixed null-destination directive must restore the moved position.");
                Assert.That(movedShadow.rotation,
                    Is.EqualTo(rotationTarget.transform.rotation).Using(QuaternionEqualityComparer.Instance),
                    "An unrelated rotation channel must continue to preview.");
            }
            finally
            {
                inactiveContext.Invalidate();
                activeContext.Invalidate();
                ROSimulator.PropertyOverrides.Value = null;
                ComputeContext.FlushInvalidates();
                handle.Dispose();
            }
        }
    
        [Test]
        public void HierarchyPreorderEvaluatesAnEarlyDeepConsumerBeforeALaterShallowTarget()
        {
            var root = CreateRoot("root");
            var earlyBranch = CreateChild(root, "early branch");
            var consumer = CreateChild(earlyBranch, "consumer");
            var moved = CreateChild(root, "moved");
            var target = CreateChild(root, "target");
            consumer.transform.position = new Vector3(1, 2, 3);
            moved.transform.position = new Vector3(11, -7, 5);
            target.transform.position = new Vector3(-13, 17, 19);
            var movedOriginalPosition = moved.transform.position;
            var targetOriginalPosition = target.transform.position;
    
            // Attach the later target's directive first. Target hierarchy preorder, rather than component or
            // source/depth ordering, must still run the early deep consumer before the later shallow target.
            AddMove(root, moved, target, fixToWorld: false, position: true, rotation: false, scale: false);
            AddMove(root, consumer, moved, fixToWorld: false, position: true, rotation: false, scale: false);
    
            var renderer = consumer.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { consumer.transform };
            var proxy = TrackObject(new GameObject("proxy")).AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { consumer.transform };
            var handle = new ShadowHandle(new Dictionary<Renderer, Renderer> { { renderer, proxy } });
            var previewContext = new PreviewContext { ShadowBoneManager = handle };
            var context = new ComputeContext("hierarchy preorder reactive move preview");
            try
            {
                using (previewContext.Activate())
                {
                    var node = new ReactiveMovePreview().Instantiate(
                        RenderGroup.For(renderer).WithData(root),
                        new[] { ((Renderer)renderer, (Renderer)proxy) },
                        context
                    ).Result;
    
                    handle.SyncPoses();
                    node.OnFrameGroup();
                    Assert.That(handle.GetExistingShadow(consumer.transform).position,
                        Is.EqualTo(movedOriginalPosition).Using(Vector3EqualityComparer.Instance),
                        "The earlier hierarchy consumer must read the later target's original pose.");
                    Assert.That(handle.GetExistingShadow(moved.transform).position,
                        Is.EqualTo(targetOriginalPosition).Using(Vector3EqualityComparer.Instance));
    
                    node.OnFrameGroup();
                    Assert.That(handle.GetExistingShadow(consumer.transform).position,
                        Is.EqualTo(targetOriginalPosition).Using(Vector3EqualityComparer.Instance),
                        "A repeated group invocation in the same simulated frame can observe the prior shadow pose.");
    
                    handle.SyncPoses();
                    node.OnFrameGroup();
                    Assert.That(handle.GetExistingShadow(consumer.transform).position,
                        Is.EqualTo(movedOriginalPosition).Using(Vector3EqualityComparer.Instance),
                        "A new simulated frame must reset the source shadow to its original pose.");
                    Assert.That(moved.transform.position,
                        Is.EqualTo(movedOriginalPosition).Using(Vector3EqualityComparer.Instance),
                        "Preview evaluation must not modify source transforms.");
                    Assert.That(target.transform.position,
                        Is.EqualTo(targetOriginalPosition).Using(Vector3EqualityComparer.Instance));
                }
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
                handle.Dispose();
            }
        }
    
        [UnityTest]
        public IEnumerator RefreshSelectsIndependentLatestWinnersForEachChannel()
        {
            var root = CreateRoot("root");
            var moved = CreateChild(root, "moved");
            var initial = CreateChild(root, "initial");
            var position = CreateChild(root, "position");
            var rotation = CreateChild(root, "rotation");
            var scale = CreateChild(root, "scale");
            initial.transform.position = new Vector3(3, -2, 7);
            initial.transform.rotation = Quaternion.Euler(17, 28, -39);
            initial.transform.localScale = new Vector3(0.7f, 1.4f, 1.8f);
            position.transform.position = new Vector3(-8, 5, 11);
            rotation.transform.rotation = Quaternion.Euler(-42, 61, 23);
            scale.transform.localScale = new Vector3(1.6f, 0.5f, 2.1f);
    
            AddMove(root, moved, initial, fixToWorld: false, position: true, rotation: true, scale: true);
            var positionController = CreateChild(root, "position controller");
            var rotationController = CreateChild(root, "rotation controller");
            var scaleController = CreateChild(root, "scale controller");
            positionController.SetActive(false);
            rotationController.SetActive(false);
            scaleController.SetActive(false);
            AddMove(positionController, moved, position, fixToWorld: false, position: true, rotation: false, scale: false);
            AddMove(rotationController, moved, rotation, fixToWorld: false, position: false, rotation: true, scale: false);
            AddMove(scaleController, moved, scale, fixToWorld: false, position: false, rotation: false, scale: true);
    
            var renderer = moved.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { moved.transform };
            var proxy = TrackObject(new GameObject("proxy")).AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { moved.transform };
            var handle = new ShadowHandle(new Dictionary<Renderer, Renderer> { { renderer, proxy } });
            var previewContext = new PreviewContext { ShadowBoneManager = handle };
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            var inactiveContext = new ComputeContext("inactive winner reactive move preview");
            var activeContext = new ComputeContext("active winner reactive move preview");
            try
            {
                IRenderFilterNode node;
                using (previewContext.Activate())
                {
                    node = new ReactiveMovePreview().Instantiate(
                        RenderGroup.For(renderer).WithData(root),
                        new[] { ((Renderer)renderer, (Renderer)proxy) },
                        inactiveContext
                    ).Result;
                    handle.SyncPoses();
                    node.OnFrameGroup();
                }
                AssertWorldChannelEquals(handle.GetExistingShadow(moved.transform), initial.transform, WorldChannel.Position,
                    "The initially active source must win before refresh.");
    
                foreach (var controller in new[] { positionController, rotationController, scaleController })
                {
                    ROSimulator.PropertyOverrides.Value = ROSimulator.PropertyOverrides.Value!.SetItem(
                        $"__ActiveSelfProxy/{controller.GetEntityId()}", 1.0f
                    );
                }
                for (var i = 0; i < 10 && !inactiveContext.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }
                Assert.That(inactiveContext.IsInvalidated, Is.True);
    
                using (previewContext.Activate())
                {
                    _ = node.Refresh(new[] { ((Renderer)renderer, (Renderer)proxy) }, activeContext, 0).Result;
                    handle.SyncPoses();
                    node.OnFrameGroup();
                }
    
                var movedShadow = handle.GetExistingShadow(moved.transform);
                AssertWorldChannelEquals(movedShadow, position.transform, WorldChannel.Position,
                    "Refresh must cache the active position winner.");
                AssertWorldChannelEquals(movedShadow, rotation.transform, WorldChannel.Rotation,
                    "Refresh must cache the active rotation winner.");
                AssertWorldChannelEquals(movedShadow, scale.transform, WorldChannel.Scale,
                    "Refresh must cache the active scale winner.");
            }
            finally
            {
                inactiveContext.Invalidate();
                activeContext.Invalidate();
                ROSimulator.PropertyOverrides.Value = null;
                ComputeContext.FlushInvalidates();
                handle.Dispose();
            }
        }
    
        [UnityTest]
        public IEnumerator UnresolvedActiveMoveDisablesOnlyItsRequestedPreviewChannelUntilDeactivated()
        {
            var root = CreateRoot("root");
            var moved = CreateChild(root, "moved");
            moved.transform.position = new Vector3(3, -2, 7);
            moved.transform.rotation = Quaternion.Euler(17, 28, -39);
            moved.transform.localScale = new Vector3(0.7f, 1.4f, 1.8f);
            var positionTarget = CreateChild(root, "position target");
            positionTarget.transform.position = new Vector3(-8, 5, 11);
            var rotationTarget = CreateChild(root, "rotation target");
            rotationTarget.transform.rotation = Quaternion.Euler(-42, 61, 23);
            var scaleTarget = CreateChild(root, "scale target");
            scaleTarget.transform.localScale = new Vector3(1.6f, 0.5f, 2.1f);
            var invalidController = CreateChild(root, "invalid controller");
            invalidController.SetActive(false);
    
            AddMove(root, moved, positionTarget, fixToWorld: false, position: true, rotation: false, scale: false);
            AddMove(root, moved, rotationTarget, fixToWorld: false, position: false, rotation: true, scale: false);
            AddMove(root, moved, scaleTarget, fixToWorld: false, position: false, rotation: false, scale: true);
            AddMove(invalidController, moved, null, fixToWorld: false, position: true, rotation: false, scale: false);
    
            var renderer = moved.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { moved.transform };
            var proxy = TrackObject(new GameObject("proxy")).AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { moved.transform };
            var handle = new ShadowHandle(new Dictionary<Renderer, Renderer> { { renderer, proxy } });
            var previewContext = new PreviewContext { ShadowBoneManager = handle };
            ROSimulator.PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            var validContext = new ComputeContext("valid reactive move preview");
            var invalidContext = new ComputeContext("unresolved reactive move preview");
            var restoredContext = new ComputeContext("restored reactive move preview");
            try
            {
                IRenderFilterNode node;
                using (previewContext.Activate())
                {
                    node = new ReactiveMovePreview().Instantiate(
                        RenderGroup.For(renderer).WithData(root),
                        new[] { ((Renderer)renderer, (Renderer)proxy) },
                        validContext
                    ).Result;
                    handle.SyncPoses();
                    node.OnFrameGroup();
                }
    
                var movedShadow = handle.GetExistingShadow(moved.transform);
                AssertWorldChannelEquals(movedShadow, positionTarget.transform, WorldChannel.Position,
                    "The earlier valid position move must preview before the unresolved move activates.");
    
                ROSimulator.PropertyOverrides.Value = ROSimulator.PropertyOverrides.Value!.SetItem(
                    $"__ActiveSelfProxy/{invalidController.GetEntityId()}", 1.0f
                );
                for (var i = 0; i < 10 && !validContext.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }
                Assert.That(validContext.IsInvalidated, Is.True);
    
                using (previewContext.Activate())
                {
                    _ = node.Refresh(new[] { ((Renderer)renderer, (Renderer)proxy) }, invalidContext, 0).Result;
                    handle.SyncPoses();
                    node.OnFrameGroup();
                }
    
                AssertWorldChannelEquals(movedShadow, moved.transform, WorldChannel.Position,
                    "The active unresolved move must restore its requested position channel instead of using an earlier target.");
                AssertWorldChannelEquals(movedShadow, rotationTarget.transform, WorldChannel.Rotation,
                    "The unresolved position move must not override the selected rotation channel.");
                AssertWorldChannelEquals(movedShadow, scaleTarget.transform, WorldChannel.Scale,
                    "The unresolved position move must not override the selected scale channel.");
    
                ROSimulator.PropertyOverrides.Value = ROSimulator.PropertyOverrides.Value!.SetItem(
                    $"__ActiveSelfProxy/{invalidController.GetEntityId()}", 0.0f
                );
                for (var i = 0; i < 10 && !invalidContext.IsInvalidated; i++)
                {
                    yield return null;
                    ComputeContext.FlushInvalidates();
                }
                Assert.That(invalidContext.IsInvalidated, Is.True);
    
                using (previewContext.Activate())
                {
                    _ = node.Refresh(new[] { ((Renderer)renderer, (Renderer)proxy) }, restoredContext, 0).Result;
                    handle.SyncPoses();
                    node.OnFrameGroup();
                }
    
                AssertWorldChannelEquals(movedShadow, positionTarget.transform, WorldChannel.Position,
                    "Deactivating the unresolved move must restore the earlier valid position target.");
                AssertWorldChannelEquals(movedShadow, rotationTarget.transform, WorldChannel.Rotation,
                    "Deactivating the unresolved move must preserve the selected rotation target.");
                AssertWorldChannelEquals(movedShadow, scaleTarget.transform, WorldChannel.Scale,
                    "Deactivating the unresolved move must preserve the selected scale target.");
            }
            finally
            {
                validContext.Invalidate();
                invalidContext.Invalidate();
                restoredContext.Invalidate();
                ROSimulator.PropertyOverrides.Value = null;
                ComputeContext.FlushInvalidates();
                handle.Dispose();
            }
        }
    
        public enum WorldChannel
        {
            Position,
            Rotation,
            Scale,
        }
    
        private static void ChangeSourceChannel(Transform source, WorldChannel channel)
        {
            switch (channel)
            {
                case WorldChannel.Position:
                    source.position += new Vector3(4, -3, 2);
                    break;
                case WorldChannel.Rotation:
                    source.rotation = Quaternion.Euler(-47, 32, 81);
                    break;
                case WorldChannel.Scale:
                    source.localScale = new Vector3(0.8f, 1.9f, 0.6f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }
    
        private static void AssertWorldChannelEquals(
            Transform actual,
            Transform expected,
            WorldChannel channel,
            string message
        )
        {
            switch (channel)
            {
                case WorldChannel.Position:
                    Assert.That(actual.position,
                        Is.EqualTo(expected.position).Using(Vector3EqualityComparer.Instance), message);
                    break;
                case WorldChannel.Rotation:
                    Assert.That(actual.rotation,
                        Is.EqualTo(expected.rotation).Using(QuaternionEqualityComparer.Instance), message);
                    break;
                case WorldChannel.Scale:
                    Assert.That(actual.lossyScale,
                        Is.EqualTo(expected.lossyScale).Using(Vector3EqualityComparer.Instance), message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }
    
        [Test]
        public void Inspector_DisablesDestinationWhenFixToWorldIsInitiallyEnabled()
        {
            var root = CreateRoot("root");
            var move = root.AddComponent<ModularAvatarReactiveMove>();
            move.FixToWorld = true;
    
            var editor = UnityEditor.Editor.CreateEditor(move, typeof(ReactiveMoveEditor));
            try
            {
                var inspector = editor.CreateInspectorGUI();
                var whereTo = inspector.Q<PropertyField>("where-to");
    
                Assert.That(whereTo.enabledSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }
    
        [Test]
        public void Inspector_DisablesDestinationForMixedFixToWorldValues()
        {
            var first = CreateRoot("first").AddComponent<ModularAvatarReactiveMove>();
            var second = CreateRoot("second").AddComponent<ModularAvatarReactiveMove>();
            second.FixToWorld = true;
    
            var editor = UnityEditor.Editor.CreateEditor(
                new Object[] { first, second },
                typeof(ReactiveMoveEditor)
            );
            try
            {
                var inspector = editor.CreateInspectorGUI();
                var whereTo = inspector.Q<PropertyField>("where-to");
    
                Assert.That(whereTo.enabledSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }
    
        [UnityTest]
        public IEnumerator Inspector_TracksExternalFixToWorldChangesAndUndo()
        {
            var root = CreateRoot("root");
            var move = root.AddComponent<ModularAvatarReactiveMove>();
            var editor = UnityEditor.Editor.CreateEditor(move, typeof(ReactiveMoveEditor));
            var window = EditorWindow.GetWindow<ReactiveMoveInspectorTestWindow>();
            try
            {
                var inspector = editor.CreateInspectorGUI();
                window.rootVisualElement.Add(inspector);
                yield return null;
    
                var whereTo = inspector.Q<PropertyField>("where-to");
                Assert.That(whereTo.enabledSelf, Is.True);
    
                var serializedMove = new SerializedObject(move);
                var fixToWorld = serializedMove.FindProperty("m_fixToWorld");
                Undo.IncrementCurrentGroup();
                var undoGroup = Undo.GetCurrentGroup();
                fixToWorld.boolValue = true;
                serializedMove.ApplyModifiedProperties();
                Undo.CollapseUndoOperations(undoGroup);
                yield return WaitForEnabledState(whereTo, false);
    
                Assert.That(whereTo.enabledSelf, Is.False);
    
                Undo.PerformUndo();
                yield return WaitForEnabledState(whereTo, true);
    
                Assert.That(whereTo.enabledSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(editor);
                window.Close();
            }
        }
    
        private sealed class ReactiveMoveInspectorTestWindow : EditorWindow
        {
        }
    
        private static IEnumerator WaitForEnabledState(VisualElement element, bool expected)
        {
            var deadline = EditorApplication.timeSinceStartup + 2;
            while (element.enabledSelf != expected && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    
        private static void AddMove(
            GameObject owner,
            GameObject toMove,
            GameObject? whereTo,
            bool fixToWorld,
            bool position,
            bool rotation,
            bool scale
        )
        {
            var move = owner.AddComponent<ModularAvatarReactiveMove>();
            move.ToMove = new AvatarObjectReference(toMove);
            move.WhereTo = whereTo == null ? new AvatarObjectReference() : new AvatarObjectReference(whereTo);
            move.FixToWorld = fixToWorld;
            move.SetsPosition = position;
            move.SetsRotation = rotation;
            move.SetsScale = scale;
        }
    
        private sealed class ShadowHandle : IShadowBoneManagerHandle, IDisposable
        {
            private readonly Dictionary<Renderer, Renderer> _proxies;
            private readonly Dictionary<Transform, Transform> _shadows = new();
    
            public ShadowHandle(Dictionary<Renderer, Renderer> proxies)
            {
                _proxies = proxies;
            }
    
            public bool HasShadow(Transform source) => _shadows.ContainsKey(source);
            public Transform GetExistingShadow(Transform source) => _shadows[source];
    
            public Transform GetBone(Transform bone)
            {
                if (bone == null) return null;
                if (_shadows.TryGetValue(bone, out var shadow)) return shadow;
    
                shadow = new GameObject($"shadow {bone.name}").transform;
                _shadows.Add(bone, shadow);
                if (bone.parent != null) shadow.SetParent(GetBone(bone.parent), false);
                shadow.localPosition = bone.localPosition;
                shadow.localRotation = bone.localRotation;
                shadow.localScale = bone.localScale;
                foreach (var proxy in _proxies.Values)
                {
                    ReplaceReferences(proxy, bone, shadow);
                }
                return shadow;
            }
    
            public void ReplaceBone(Renderer originalRenderer, Transform original, Transform replacement)
            {
                if (_proxies.TryGetValue(originalRenderer, out var proxy))
                {
                    ReplaceReferences(proxy, original, replacement);
                }
            }
    
            public void SyncPoses()
            {
                foreach (var source in _shadows.Keys.ToArray()) EnsureShadowHierarchy(source);
                foreach (var (source, shadow) in _shadows)
                {
                    shadow.localPosition = source.localPosition;
                    shadow.localRotation = source.localRotation;
                    shadow.localScale = source.localScale;
                }
            }
    
            public void Dispose()
            {
                foreach (var shadow in _shadows.Values)
                {
                    if (shadow != null) Object.DestroyImmediate(shadow.gameObject);
                }
            }
    
            private static void ReplaceReferences(Renderer renderer, Transform original, Transform replacement)
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.bones = skinned.bones.Select(bone => bone == original ? replacement : bone).ToArray();
                    if (skinned.rootBone == original) skinned.rootBone = replacement;
                }
                if (renderer.probeAnchor == original) renderer.probeAnchor = replacement;
                if (renderer.transform.parent == original) renderer.transform.SetParent(replacement, false);
            }
    
            private void EnsureShadowHierarchy(Transform source)
            {
                var shadow = GetBone(source);
                if (source.parent == null)
                {
                    shadow.SetParent(null, false);
                    return;
                }
    
                EnsureShadowHierarchy(source.parent);
                shadow.SetParent(GetBone(source.parent), false);
            }
        }
    }
}
