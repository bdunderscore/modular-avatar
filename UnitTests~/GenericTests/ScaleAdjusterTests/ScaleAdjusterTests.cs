using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using ScaleAdjusterPreview = nadena.dev.modular_avatar.core.editor.ScaleAdjusterPreview;
using ScaleAdjusterPreviewNode = nadena.dev.modular_avatar.core.editor.ScaleAdjusterPreviewNode;

namespace UnitTests.ScaleAdjusterTests
{
    public class ScaleAdjusterTests : TestBase
    {
        [Test]
        public void ScaleAdjuster_WorksOnNonHumanoidRig(
            [Values("Generic.prefab", "GenericShapell.prefab")] string prefabName
        )
        {
            var prefab = CreatePrefab(prefabName);
            AvatarProcessor.ProcessAvatar(prefab);
        }
        
        [Test]
        public void ScaleAdjuster_FixesHumanAvatarDescription()
        {
            var prefab = CreatePrefab("ScaleAdjuster_FixesHumanAvatarDescription.prefab");

            AvatarProcessor.ProcessAvatar(prefab);

            var animator = prefab.GetComponent<Animator>();
            var humanDesc = animator.avatar.humanDescription;

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var headDesc = humanDesc.skeleton.First(b => b.name == head.gameObject.name);
            
            Assert.That(Vector3.Distance(headDesc.position, head.localPosition), Is.LessThan(0.001f));
        }

        [Test]
        public void ScaleAdjusterPreview_TracksInactiveSkinnedMeshRenderers()
        {
            var root = CreateRoot("Avatar");
            var bone = CreateChild(root, "Bone");
            bone.AddComponent<ModularAvatarScaleAdjuster>();

            var rendererObject = CreateChild(root, "Inactive Renderer");
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            rendererObject.SetActive(false);

            var meshRendererObject = CreateChild(root, "Mesh Renderer");
            var meshRenderer = meshRendererObject.AddComponent<MeshRenderer>();
            meshRendererObject.AddComponent<MeshFilter>();

            var context = new ComputeContext("ScaleAdjusterPreview target test");
            try
            {
                var group = new ScaleAdjusterPreview().GetTargetGroups(context)
                    .Single(g => g.GetData<GameObject>() == root);

                Assert.That(group.Renderers.Contains(renderer), Is.True);
                Assert.That(group.Renderers.Contains(meshRenderer), Is.False);
            }
            finally
            {
                context.Invalidate();
                ComputeContext.FlushInvalidates();
            }
        }

        [Test]
        public void ScaleAdjusterPreview_MatchesBuildBoneMappingAndTransfersSmallRotations()
        {
            var root = CreateRoot("Avatar");
            var bone = CreateChild(root, "Bone");
            var childBone = CreateChild(bone, "Child Bone");
            var adjuster = bone.AddComponent<ModularAvatarScaleAdjuster>();
            adjuster.Scale = new Vector3(2, 3, 4);

            var rendererObject = CreateChild(bone, "Renderer");
            var original = rendererObject.AddComponent<SkinnedMeshRenderer>();
            original.rootBone = bone.transform;
            original.bones = new[] { bone.transform };

            var proxyObject = TrackObject(new GameObject("Proxy Renderer"));
            proxyObject.transform.SetParent(bone.transform, false);
            var proxy = proxyObject.AddComponent<SkinnedMeshRenderer>();
            proxy.rootBone = bone.transform;
            proxy.bones = new[] { bone.transform };
            proxy.probeAnchor = bone.transform;

            var unlistedProxyObject = TrackObject(new GameObject("Unlisted Proxy Renderer"));
            var unlistedProxy = unlistedProxyObject.AddComponent<SkinnedMeshRenderer>();
            unlistedProxy.rootBone = bone.transform;
            unlistedProxy.bones = new[] { bone.transform, childBone.transform };
            unlistedProxy.probeAnchor = bone.transform;
            var group = RenderGroup.For(original).WithData(root);

            var rendererProxies = new Dictionary<Renderer, Renderer>();
            rendererProxies[original] = proxy;
            var fixture = new ShadowBoneTestFixture();
            var previewContext = new PreviewContext
            {
                ShadowBoneManager = fixture.Handle
            };
            ScaleAdjusterPreviewNode node = null;

            try
            {
                using (previewContext.Activate())
                {
                    node = fixture.ExecuteInStageSync(
                        rendererProxies,
                        () => new ScaleAdjusterPreviewNode(
                            ComputeContext.NullContext,
                            group,
                            new[] { ((Renderer)original, (Renderer)proxy) }
                        )
                    );

                    Assert.That(node.WhatChanged, Is.EqualTo(RenderAspects.Shapes));
                    var adjustedBone = proxy.bones[0];
                    Assert.That(adjustedBone, Is.Not.SameAs(bone.transform));
                    Assert.That(adjustedBone.localScale, Is.EqualTo(adjuster.Scale));
                    Assert.That(proxy.rootBone, Is.SameAs(adjustedBone));
                    Assert.That(proxy.probeAnchor, Is.SameAs(adjustedBone));
                    Assert.That(proxy.transform.parent, Is.SameAs(adjustedBone));


                    Assert.That(unlistedProxy.rootBone, Is.SameAs(bone.transform));
                    Assert.That(unlistedProxy.bones, Is.EqualTo(new[] { bone.transform, childBone.transform }));
                    Assert.That(unlistedProxy.probeAnchor, Is.SameAs(bone.transform));

                    var transferredBone = adjustedBone.parent;
                    fixture.SyncPoses();
                    transferredBone.hasChanged = false;
                    node.OnFrameGroup();
                    fixture.SyncPoses();
                    Assert.That(transferredBone.hasChanged, Is.False);

                    bone.transform.localRotation = Quaternion.AngleAxis(0.01f, Vector3.right);
                    node.OnFrameGroup();
                    fixture.SyncPoses();
                    Assert.That(bone.transform.rotation.x, Is.Not.EqualTo(0));
                    Assert.That(transferredBone.hasChanged, Is.True);
                    AssertQuaternionExactlyEqual(bone.transform.rotation, transferredBone.rotation);
                }
            }
            finally
            {
                node?.Dispose();
                fixture.Dispose();
            }
        }

        [Test]
        public void ScaleAdjusterPreview_RefreshNodesOwnIndependentProxyBones()
        {
            var root = CreateRoot("Avatar");
            var bone = CreateChild(root, "Bone");
            var adjuster = bone.AddComponent<ModularAvatarScaleAdjuster>();
            adjuster.Scale = new Vector3(2, 2, 2);

            var rendererObject = CreateChild(root, "Renderer");
            var original = rendererObject.AddComponent<SkinnedMeshRenderer>();
            original.bones = new[] { bone.transform };

            var group = RenderGroup.For(original).WithData(root, (a, b) => a == b);
            var rendererProxies = new Dictionary<Renderer, Renderer>();
            var firstFixture = new ShadowBoneTestFixture();
            ShadowBoneTestFixture secondFixture = null;
            ShadowBoneTestFixture thirdFixture = null;
            ScaleAdjusterPreviewNode firstNode = null;
            IRenderFilterNode secondNode = null;
            IRenderFilterNode thirdNode = null;

            try
            {
                var firstProxyObject = TrackObject(new GameObject("First Proxy Renderer"));
                var firstProxy = firstProxyObject.AddComponent<SkinnedMeshRenderer>();
                firstProxy.bones = new[] { bone.transform };
                rendererProxies[original] = firstProxy;
                var firstPreviewContext = new PreviewContext
                {
                    ShadowBoneManager = firstFixture.Handle
                };
                using (firstPreviewContext.Activate())
                {
                    firstNode = firstFixture.ExecuteInStageSync(
                        rendererProxies,
                        () => new ScaleAdjusterPreviewNode(
                            ComputeContext.NullContext,
                            group,
                            new[] { ((Renderer)original, (Renderer)firstProxy) }
                        )
                    );
                }

                var firstProxyBone = firstProxy.bones[0];

                var secondProxyObject = TrackObject(new GameObject("Second Proxy Renderer"));
                var secondProxy = secondProxyObject.AddComponent<SkinnedMeshRenderer>();
                secondProxy.bones = new[] { bone.transform };
                adjuster.Scale = new Vector3(3, 3, 3);
                rendererProxies[original] = secondProxy;
                secondFixture = new ShadowBoneTestFixture();
                var secondPreviewContext = new PreviewContext
                {
                    ShadowBoneManager = secondFixture.Handle
                };
                using (secondPreviewContext.Activate())
                {
                    secondNode = secondFixture.ExecuteInStageSync(
                        rendererProxies,
                        () => firstNode.Refresh(
                            new[] { ((Renderer)original, (Renderer)secondProxy) },
                            ComputeContext.NullContext,
                            RenderAspects.Shapes
                        ).Result
                    );
                }

                Assert.That(secondNode, Is.Not.SameAs(firstNode));
                var secondProxyBone = secondProxy.bones[0];

                var thirdProxyObject = TrackObject(new GameObject("Third Proxy Renderer"));
                var thirdProxy = thirdProxyObject.AddComponent<SkinnedMeshRenderer>();
                thirdProxy.bones = new[] { bone.transform };
                adjuster.Scale = new Vector3(4, 4, 4);
                rendererProxies[original] = thirdProxy;
                thirdFixture = new ShadowBoneTestFixture();
                var thirdPreviewContext = new PreviewContext
                {
                    ShadowBoneManager = thirdFixture.Handle
                };
                using (thirdPreviewContext.Activate())
                {
                    thirdNode = thirdFixture.ExecuteInStageSync(
                        rendererProxies,
                        () => secondNode.Refresh(
                            new[] { ((Renderer)original, (Renderer)thirdProxy) },
                            ComputeContext.NullContext,
                            RenderAspects.Shapes
                        ).Result
                    );
                }

                Assert.That(thirdNode, Is.Not.SameAs(secondNode));
                var thirdProxyBone = thirdProxy.bones[0];

                Assert.That(firstProxyBone, Is.Not.SameAs(secondProxyBone));
                Assert.That(secondProxyBone, Is.Not.SameAs(thirdProxyBone));
                Assert.That(firstProxyBone == null, Is.False);
                Assert.That(secondProxyBone == null, Is.False);
                Assert.That(thirdProxyBone == null, Is.False);

                firstNode.Dispose();
                firstNode = null;
                Assert.That(firstProxyBone == null, Is.True);
                Assert.That(secondProxyBone == null, Is.False);
                Assert.That(thirdProxyBone == null, Is.False);

                secondNode.Dispose();
                secondNode = null;
                Assert.That(secondProxyBone == null, Is.True);
                Assert.That(thirdProxyBone == null, Is.False);

                thirdNode.Dispose();
                thirdNode = null;
                Assert.That(thirdProxyBone == null, Is.True);
            }
            finally
            {
                thirdNode?.Dispose();
                secondNode?.Dispose();
                firstNode?.Dispose();
                thirdFixture?.Dispose();
                secondFixture?.Dispose();
                firstFixture.Dispose();
            }
        }


        private static void AssertQuaternionExactlyEqual(Quaternion expected, Quaternion actual)
        {
            var equal = expected.x == actual.x
                        && expected.y == actual.y
                        && expected.z == actual.z
                        && expected.w == actual.w;
            var doubleCoverEqual = expected.x == -actual.x
                                   && expected.y == -actual.y
                                   && expected.z == -actual.z
                                   && expected.w == -actual.w;

            Assert.That(equal || doubleCoverEqual, Is.True,
                $"Expected {expected} or its negation, but was {actual}");
        }
    }
}
