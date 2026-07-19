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
            var adjuster = bone.AddComponent<ModularAvatarScaleAdjuster>();
            adjuster.Scale = new Vector3(2, 3, 4);

            var rendererObject = CreateChild(bone, "Renderer");
            var original = rendererObject.AddComponent<SkinnedMeshRenderer>();
            original.rootBone = bone.transform;
            original.bones = new[] { bone.transform };

            var proxyObject = TrackObject(new GameObject("Proxy Renderer"));
            var proxy = proxyObject.AddComponent<SkinnedMeshRenderer>();
            proxy.rootBone = bone.transform;
            proxy.bones = new[] { bone.transform };

            var group = RenderGroup.For(original).WithData(root, (a, b) => a == b);
            var node = new ScaleAdjusterPreviewNode(
                ComputeContext.NullContext,
                group,
                new[] { ((Renderer)original, (Renderer)proxy) }
            );

            try
            {
                Assert.That(node.WhatChanged, Is.EqualTo(RenderAspects.Shapes));
                node.OnFrameGroup();
                node.OnFrame(original, proxy);

                var adjustedBone = proxy.bones[0];
                Assert.That(adjustedBone, Is.Not.SameAs(bone.transform));
                Assert.That(adjustedBone.localScale, Is.EqualTo(adjuster.Scale));
                Assert.That(proxy.rootBone, Is.SameAs(bone.transform));
                Assert.That(proxy.transform.parent, Is.Null);

                var transferredBone = adjustedBone.parent;
                transferredBone.hasChanged = false;
                node.OnFrameGroup();
                Assert.That(transferredBone.hasChanged, Is.False);

                bone.transform.localRotation = Quaternion.AngleAxis(0.01f, Vector3.right);
                node.OnFrameGroup();
                Assert.That(bone.transform.rotation.x, Is.Not.EqualTo(0));
                Assert.That(transferredBone.hasChanged, Is.True);
                AssertQuaternionExactlyEqual(bone.transform.rotation, transferredBone.rotation);
            }
            finally
            {
                node.Dispose();
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

            var firstProxyObject = TrackObject(new GameObject("First Proxy Renderer"));
            var firstProxy = firstProxyObject.AddComponent<SkinnedMeshRenderer>();
            firstProxy.bones = new[] { bone.transform };

            var group = RenderGroup.For(original).WithData(root, (a, b) => a == b);
            ScaleAdjusterPreviewNode firstNode = new ScaleAdjusterPreviewNode(
                ComputeContext.NullContext,
                group,
                new[] { ((Renderer)original, (Renderer)firstProxy) }
            );
            IRenderFilterNode secondNode = null;
            IRenderFilterNode thirdNode = null;

            try
            {
                firstNode.OnFrame(original, firstProxy);
                var firstProxyBone = firstProxy.bones[0];

                var unchangedProxyObject = TrackObject(new GameObject("Unchanged Proxy Renderer"));
                var unchangedProxy = unchangedProxyObject.AddComponent<SkinnedMeshRenderer>();
                unchangedProxy.bones = new[] { bone.transform };
                var unchangedNode = firstNode.Refresh(
                    new[] { ((Renderer)original, (Renderer)unchangedProxy) },
                    ComputeContext.NullContext,
                    RenderAspects.Shapes
                ).Result;
                Assert.That(unchangedNode, Is.SameAs(firstNode));
                Assert.That(unchangedNode.WhatChanged, Is.EqualTo((RenderAspects) 0));
                unchangedNode.OnFrame(original, unchangedProxy);
                Assert.That(unchangedProxy.bones[0], Is.SameAs(firstProxyBone));

                var secondProxyObject = TrackObject(new GameObject("Second Proxy Renderer"));
                var secondProxy = secondProxyObject.AddComponent<SkinnedMeshRenderer>();
                secondProxy.bones = new[] { bone.transform };
                adjuster.Scale = new Vector3(3, 3, 3);
                secondNode = firstNode.Refresh(
                    new[] { ((Renderer)original, (Renderer)secondProxy) },
                    ComputeContext.NullContext,
                    RenderAspects.Shapes
                ).Result;
                Assert.That(secondNode, Is.Not.SameAs(firstNode));
                secondNode.OnFrame(original, secondProxy);
                var secondProxyBone = secondProxy.bones[0];

                var thirdProxyObject = TrackObject(new GameObject("Third Proxy Renderer"));
                var thirdProxy = thirdProxyObject.AddComponent<SkinnedMeshRenderer>();
                thirdProxy.bones = new[] { bone.transform };
                adjuster.Scale = new Vector3(4, 4, 4);
                thirdNode = secondNode.Refresh(
                    new[] { ((Renderer)original, (Renderer)thirdProxy) },
                    ComputeContext.NullContext,
                    RenderAspects.Shapes
                ).Result;
                Assert.That(thirdNode, Is.Not.SameAs(secondNode));
                thirdNode.OnFrame(original, thirdProxy);
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
            }
        }

        [Test]
        public void ScaleAdjusterPreview_RefreshesForRendererBoneAndWorldTransformChanges()
        {
            var root = CreateRoot("Avatar");
            var bone = CreateChild(root, "Bone");
            var otherBone = CreateChild(root, "Other Bone");
            bone.AddComponent<ModularAvatarScaleAdjuster>();

            var rendererObject = CreateChild(root, "Renderer");
            var original = rendererObject.AddComponent<SkinnedMeshRenderer>();
            original.bones = new[] { bone.transform };

            var proxyObject = TrackObject(new GameObject("Proxy Renderer"));
            var proxy = proxyObject.AddComponent<SkinnedMeshRenderer>();
            proxy.bones = new[] { bone.transform };

            var group = RenderGroup.For(original).WithData(root, (a, b) => a == b);
            var node = new ScaleAdjusterPreviewNode(
                ComputeContext.NullContext,
                group,
                new[] { ((Renderer)original, (Renderer)proxy) }
            );

            try
            {
                var changedBonesProxyObject = TrackObject(new GameObject("Changed Bones Proxy Renderer"));
                var changedBonesProxy = changedBonesProxyObject.AddComponent<SkinnedMeshRenderer>();
                changedBonesProxy.bones = new[] { otherBone.transform };
                AssertCreatesNewNode(new[] { ((Renderer)original, (Renderer)changedBonesProxy) });

                var newParent = CreateChild(root, "New Parent");
                newParent.transform.localPosition = Vector3.right;
                bone.transform.SetParent(newParent.transform, false);
                AssertCreatesNewNode(new[] { ((Renderer)original, (Renderer)proxy) });
                bone.transform.SetParent(root.transform, false);

                bone.transform.localRotation = Quaternion.Euler(0, 1, 0);
                AssertCreatesNewNode(new[] { ((Renderer)original, (Renderer)proxy) });
                bone.transform.localRotation = Quaternion.identity;

                bone.transform.localScale = new Vector3(1.01f, 1, 1);
                AssertCreatesNewNode(new[] { ((Renderer)original, (Renderer)proxy) });
                bone.transform.localScale = Vector3.one;

                var secondRendererObject = CreateChild(root, "Second Renderer");
                var secondOriginal = secondRendererObject.AddComponent<SkinnedMeshRenderer>();
                secondOriginal.bones = new[] { bone.transform };
                var secondProxyObject = TrackObject(new GameObject("Second Proxy Renderer"));
                var secondProxy = secondProxyObject.AddComponent<SkinnedMeshRenderer>();
                secondProxy.bones = new[] { bone.transform };
                AssertCreatesNewNode(new[]
                {
                    ((Renderer)original, (Renderer)proxy),
                    ((Renderer)secondOriginal, (Renderer)secondProxy)
                });
            }
            finally
            {
                node.Dispose();
            }

            void AssertCreatesNewNode(IEnumerable<(Renderer, Renderer)> proxyPairs)
            {
                var refreshedNode = node.Refresh(
                    proxyPairs,
                    ComputeContext.NullContext,
                    RenderAspects.Shapes
                ).Result;
                try
                {
                    Assert.That(refreshedNode, Is.Not.SameAs(node));
                }
                finally
                {
                    refreshedNode.Dispose();
                }
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
