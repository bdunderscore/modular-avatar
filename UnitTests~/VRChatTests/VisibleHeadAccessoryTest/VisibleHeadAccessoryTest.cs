#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace UnitTests.VisibleHeadAccessoryTest
{
    internal class VisibleHeadAccessoryTest : TestBase
    {
        [Test]
        public void Test()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone, "ShapellAvatarVRC must have a Head bone");

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0),
            };
            mesh.triangles = new int[] { 0, 1, 2 };

            using var bpv = new NativeArray<byte>(new byte[] { 1, 1, 1 }, Allocator.Temp);
            using var bws = new NativeArray<BoneWeight1>(new BoneWeight1[]
            {
                new BoneWeight1 { boneIndex = 0, weight = 1 },
                new BoneWeight1 { boneIndex = 1, weight = 1 },
                new BoneWeight1 { boneIndex = 0, weight = 1 },
            }, Allocator.Temp);
            mesh.SetBoneWeights(bpv, bws);

            mesh.bindposes = new Matrix4x4[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            // -- Verify HeadChop --
            var headChopObj = root.transform.Find("VHA HeadChop");
            Assert.NotNull(headChopObj, "VHA HeadChop object should exist");
            var headChop = headChopObj.GetComponent<VRCHeadChop>();
            Assert.NotNull(headChop, "VHA HeadChop component should exist");

            var targets = headChop.targetBones.Select(b => b.transform).ToList();

            Assert.IsTrue(targets.Contains(boneO1), "HeadChop should reference BoneO1");

            var headClone = boneO1.parent.Find("Head (VHA Clone)");
            Assert.NotNull(headClone, "Head clone bone should exist under Head");
            Assert.IsTrue(targets.Contains(headClone), "HeadChop should reference Head clone");


            // -- Verify SMR bone references --
            Assert.IsTrue(
                smr.bones.Any(b => b.name.Contains("(VHA Clone)")),
                "SMR should reference at least one VHA Clone bone"
            );

            // -- Verify processed mesh bone weights --
            var processedMesh = smr.sharedMesh;
            Assert.AreNotSame(mesh, processedMesh, "Mesh should have been replaced");

            var newBpw = processedMesh.GetAllBoneWeights();
            var newBpv = processedMesh.GetBonesPerVertex();

            // The original head bone stays in the SMR bones array and original vertices
            // still reference it. Only cloned vertices must point at the head clone instead.
            var origHeadBoneIndex = System.Array.IndexOf(smr.bones, headBone);
            // Clones start at vertex index 3 (after 3 originals), each with 1 weight
            Assert.AreNotEqual(origHeadBoneIndex, newBpw[3].boneIndex,
                "Clone V0 should not reference original Head");
            Assert.AreNotEqual(origHeadBoneIndex, newBpw[4].boneIndex,
                "Clone V2 should not reference original Head");

            Assert.AreEqual(5, newBpv.Length, "Expected 3 original + 2 cloned vertices");

            int wIdx = 0;
            Assert.AreEqual(1, newBpv[0]);
            Assert.AreEqual(0, newBpw[wIdx].boneIndex, "Original V0 should still reference Head");
            wIdx++;
            Assert.AreEqual(1, newBpv[1]);
            Assert.AreEqual(1, newBpw[wIdx].boneIndex, "Original V1 should reference BoneO1");
            wIdx++;
            Assert.AreEqual(1, newBpv[2]);
            Assert.AreEqual(0, newBpw[wIdx].boneIndex, "Original V2 should still reference Head");
            wIdx++;

            Assert.AreEqual(1, newBpv[3]);
            var cloneBoneIndex = newBpw[wIdx].boneIndex;
            Assert.AreNotEqual(0, cloneBoneIndex, "Clone of V0 should not reference original Head");
            var cloneBone = smr.bones[cloneBoneIndex];
            Assert.AreEqual(headClone, cloneBone, "Clone V0 should reference Head clone transform");
            wIdx++;

            Assert.AreEqual(1, newBpv[4]);
            Assert.AreEqual(cloneBoneIndex, newBpw[wIdx].boneIndex,
                "Clone of V2 should reference same clone bone as V0 (deduplication)");
            wIdx++;
        }

        [Test]
        public void TestMixedAndNonMixedTriangles()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            // 6 vertices: V0,V2,V4 → Head(hidden), V1,V3,V5 → BoneO1(visible)
            // 4 triangles: 2 mixed (T0, T1), 2 non-mixed (T2 all-hidden, T3 all-visible)
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0), // V0: Head
                new Vector3(1, 0, 0), // V1: BoneO1
                new Vector3(0, 1, 0), // V2: Head
                new Vector3(1, 1, 0), // V3: BoneO1
                new Vector3(0, 2, 0), // V4: Head
                new Vector3(1, 2, 0), // V5: BoneO1
            };
            mesh.subMeshCount = 2;
            // Submesh 0: mixed triangles
            mesh.SetIndices(new int[] { 0, 1, 2, 3, 4, 5 }, MeshTopology.Triangles, 0);
            // Submesh 1: non-mixed triangles (all-head and all-visible)
            mesh.SetIndices(new int[] { 0, 2, 4, 1, 3, 5 }, MeshTopology.Triangles, 1);

            using var bpv = new NativeArray<byte>(new byte[] { 1, 1, 1, 1, 1, 1 }, Allocator.Temp);
            using var bws = new NativeArray<BoneWeight1>(new BoneWeight1[]
            {
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V0
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V1
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V2
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V3
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V4
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V5
            }, Allocator.Temp);
            mesh.SetBoneWeights(bpv, bws);

            mesh.bindposes = new Matrix4x4[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            // -- HeadChop --
            var headChopObj = root.transform.Find("VHA HeadChop");
            Assert.NotNull(headChopObj);
            var headChop = headChopObj.GetComponent<VRCHeadChop>();
            Assert.NotNull(headChop);

            var targets = headChop.targetBones.Select(b => b.transform).ToList();
            Assert.IsTrue(targets.Contains(boneO1), "HeadChop should reference BoneO1");

            var headClone = boneO1.parent.Find("Head (VHA Clone)");
            Assert.NotNull(headClone);
            Assert.IsTrue(targets.Contains(headClone), "HeadChop should reference Head clone");


            // -- SMR bones --
            Assert.IsTrue(smr.bones.Any(b => b.name.Contains("(VHA Clone)")),
                "SMR should reference a clone bone");

            // -- Processed mesh --
            var processedMesh = smr.sharedMesh;
            Assert.AreNotSame(mesh, processedMesh);
            Assert.AreEqual(2, processedMesh.subMeshCount);

            var newBpw = processedMesh.GetAllBoneWeights();
            var newBpv = processedMesh.GetBonesPerVertex();

            // 6 originals + 3 clones (V0, V2, V4 had non-visible weights in mixed triangles)
            int origCount = 6;
            int expectedCloneCount = 3;
            Assert.AreEqual(origCount + expectedCloneCount, newBpv.Length);

            var origHeadIndex = System.Array.IndexOf(smr.bones, headBone);

            // Original vertices keep their original bone indices
            for (int v = 0; v < origCount; v++)
            {
                int expectedBone = (v % 2 == 0) ? 0 : 1; // even → Head, odd → BoneO1
                Assert.AreEqual(expectedBone, newBpw[v].boneIndex,
                    "Original V" + v + " should keep original bone " + expectedBone);
            }

            // Cloned vertices must NOT reference the original head bone
            for (int v = origCount; v < newBpv.Length; v++)
            {
                Assert.AreNotEqual(origHeadIndex, newBpw[v].boneIndex,
                    "Clone V" + v + " should not reference original Head");
            }

            // All clones should reference the same deduplicated head clone bone
            var deduplicatedBoneIndex = newBpw[origCount].boneIndex;
            for (int v = origCount + 1; v < newBpv.Length; v++)
            {
                Assert.AreEqual(deduplicatedBoneIndex, newBpw[v].boneIndex,
                    "All clones should reference the same deduplicated clone bone");
            }

            // The head clone transform should be the one in the SMR bones array
            var cloneBone = smr.bones[deduplicatedBoneIndex];
            Assert.AreEqual(headClone, cloneBone);

            // -- Verify submesh indices --
            // Submesh 0 (mixed triangles) should reference clones for Head vertices
            var sm0 = processedMesh.GetIndices(0);
            // T0: [V0', V1, V2'] → V0→clone(6), V1→1, V2→clone(7)
            // T1: [V3, V4', V5] → V3→3, V4→clone(8), V5→5
            int[] expectedSm0 = { 6, 1, 7, 3, 8, 5 };
            Assert.AreEqual(expectedSm0, sm0, "Submesh 0 indices should use clones for Head vertices");

            // Submesh 1 (non-mixed) should reference original vertices unchanged
            var sm1 = processedMesh.GetIndices(1);
            int[] expectedSm1 = { 0, 2, 4, 1, 3, 5 };
            Assert.AreEqual(expectedSm1, sm1, "Submesh 1 indices should remain unchanged");
        }

        [Test]
        public void TestNullMesh()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone, "ShapellAvatarVRC must have a Head bone");

            var boneO1 = CreateChild(headBone.gameObject, "BoneO1").transform;
            boneO1.gameObject.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var nullSmr = new GameObject("Null SMR").AddComponent<SkinnedMeshRenderer>();
            nullSmr.transform.SetParent(boneO1, false);
            nullSmr.sharedMesh = null;
            nullSmr.bones = new Transform[] { headBone };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            var headChop = root.transform.Find("VHA HeadChop");
            Assert.NotNull(headChop, "VHA HeadChop should be created even with null mesh present");
        }

        [Test]
        public void TestMixedAndNonMixedSameSubmesh()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            // 6 vertices: V0,V2,V4 → Head(hidden), V1,V3,V5 → BoneO1(visible)
            // Single submesh with 4 triangles mixing visible and hidden
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0), // V0: Head
                new Vector3(1, 0, 0), // V1: BoneO1
                new Vector3(0, 1, 0), // V2: Head
                new Vector3(1, 1, 0), // V3: BoneO1
                new Vector3(0, 2, 0), // V4: Head (only in all-Head triangle)
                new Vector3(1, 2, 0), // V5: BoneO1 (only in all-visible triangle)
            };
            // T0: mixed, T1: mixed, T2: all-Head (unchanged), T3: all-visible (unchanged)
            mesh.triangles = new int[]
            {
                0, 1, 2, // T0: mixed (Head, BoneO1, Head) → V0,V2 cloned
                1, 0, 3, // T1: mixed (BoneO1, Head, BoneO1) → V0 cloned (dedup)
                0, 2, 4, // T2: all Head → NOT mixed, preserved
                1, 3, 5, // T3: all BoneO1 → NOT mixed, preserved
            };

            using var bpv = new NativeArray<byte>(new byte[] { 1, 1, 1, 1, 1, 1 }, Allocator.Temp);
            using var bws = new NativeArray<BoneWeight1>(new BoneWeight1[]
            {
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V0
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V1
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V2
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V3
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V4
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V5
            }, Allocator.Temp);
            mesh.SetBoneWeights(bpv, bws);

            mesh.bindposes = new Matrix4x4[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            // -- HeadChop --
            var headChopObj = root.transform.Find("VHA HeadChop");
            Assert.NotNull(headChopObj);
            var headChop = headChopObj.GetComponent<VRCHeadChop>();
            Assert.NotNull(headChop);

            var targets = headChop.targetBones.Select(b => b.transform).ToList();
            Assert.IsTrue(targets.Contains(boneO1), "HeadChop should reference BoneO1");

            var headClone = boneO1.parent.Find("Head (VHA Clone)");
            Assert.NotNull(headClone);
            Assert.IsTrue(targets.Contains(headClone), "HeadChop should reference Head clone");


            // -- Processed mesh --
            var processedMesh = smr.sharedMesh;
            Assert.AreNotSame(mesh, processedMesh);
            Assert.AreEqual(1, processedMesh.subMeshCount);

            var newBpw = processedMesh.GetAllBoneWeights();
            var newBpv = processedMesh.GetBonesPerVertex();

            // 6 originals + 2 clones (V0 and V2 are Head-vertices in mixed triangles)
            int origCount = 6;
            int expectedCloneCount = 2;
            Assert.AreEqual(origCount + expectedCloneCount, newBpv.Length);

            // Cloned vertices must not reference original Head
            var origHeadIndex = System.Array.IndexOf(smr.bones, headBone);
            for (int v = origCount; v < newBpv.Length; v++)
                Assert.AreNotEqual(origHeadIndex, newBpw[v].boneIndex,
                    "Clone should not reference original Head");

            // All clones share the same deduplicated head clone bone
            var cloneBoneIndex = newBpw[origCount].boneIndex;
            for (int v = origCount + 1; v < newBpv.Length; v++)
                Assert.AreEqual(cloneBoneIndex, newBpw[v].boneIndex,
                    "Clones should share the same clone bone");

            // -- Verify triangle indices --
            var indices = processedMesh.GetIndices(0);
            // Expected: T0 uses clones for V0,V2, T1 uses clone for V0,
            // T2 and T3 stay on original vertices
            int[] expected = {
                6, 1, 7,    // T0: V0'(6), V1(1), V2'(7)
                1, 6, 3,    // T1: V1(1), V0'(6), V3(3)
                0, 2, 4,    // T2: all originals (unchanged)
                1, 3, 5,    // T3: all originals (unchanged)
            };
            Assert.AreEqual(expected, indices,
                "Mixed triangles should use clones, non-mixed triangles should be untouched");
        }

        [Test]
        public void TestQuadMesh()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            // 4 vertices forming 1 quad: V0=Head, V1=BoneO1, V2=BoneO1, V3=Head
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0), // V0: Head
                new Vector3(1, 0, 0), // V1: BoneO1
                new Vector3(2, 0, 0), // V2: BoneO1
                new Vector3(3, 0, 0), // V3: Head
            };

            using var bpv = new NativeArray<byte>(new byte[] { 1, 1, 1, 1 }, Allocator.Temp);
            using var bws = new NativeArray<BoneWeight1>(new BoneWeight1[]
            {
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V0: Head
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V1: BoneO1
                new BoneWeight1 { boneIndex = 1, weight = 1 }, // V2: BoneO1
                new BoneWeight1 { boneIndex = 0, weight = 1 }, // V3: Head
            }, Allocator.Temp);
            mesh.SetBoneWeights(bpv, bws);

            mesh.bindposes = new Matrix4x4[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };

            mesh.subMeshCount = 1;
            mesh.SetIndices(new int[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            var processedMesh = smr.sharedMesh;
            Assert.AreNotSame(mesh, processedMesh);
            Assert.AreEqual(1, processedMesh.subMeshCount);

            var newBpw = processedMesh.GetAllBoneWeights();
            var newBpv = processedMesh.GetBonesPerVertex();

            // 4 originals + 2 clones (V0 and V3 are non-visible in the mixed quad)
            Assert.AreEqual(6, newBpv.Length, "Expected 4 original + 2 cloned vertices");

            var origHeadIndex = System.Array.IndexOf(smr.bones, headBone);

            // Original vertices keep their bone indices
            Assert.AreEqual(0, newBpw[0].boneIndex); // V0: Head
            Assert.AreEqual(1, newBpw[1].boneIndex); // V1: BoneO1
            Assert.AreEqual(1, newBpw[2].boneIndex); // V2: BoneO1
            Assert.AreEqual(0, newBpw[3].boneIndex); // V3: Head

            // Clones (indices 4,5) must not reference original Head
            Assert.AreNotEqual(origHeadIndex, newBpw[4].boneIndex,
                "Clone of V0 should not reference original Head");
            Assert.AreNotEqual(origHeadIndex, newBpw[5].boneIndex,
                "Clone of V3 should not reference original Head");

            // Both clones share the same deduplicated head clone bone
            Assert.AreEqual(newBpw[4].boneIndex, newBpw[5].boneIndex,
                "All clones should reference the same deduplicated clone bone");

            // -- Quad indices --
            var indices = processedMesh.GetIndices(0);
            var topology = processedMesh.GetTopology(0);
            Assert.AreEqual(MeshTopology.Quads, topology);
            // Original quad [0,1,2,3] → [4,1,2,5] (V0→clone, V3→clone)
            int[] expected = { 4, 1, 2, 5 };
            Assert.AreEqual(expected, indices);
        }

        [Test]
        public void TestPBBlockerOnClones()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            // Simple mixed triangle mesh
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0), // V0: Head
                new Vector3(1, 0, 0), // V1: BoneO1
                new Vector3(0, 1, 0), // V2: Head
            };
            mesh.triangles = new int[] { 0, 1, 2 };

            using var bpv = new NativeArray<byte>(new byte[] { 1, 1, 1 }, Allocator.Temp);
            using var bws = new NativeArray<BoneWeight1>(new BoneWeight1[]
            {
                new BoneWeight1 { boneIndex = 0, weight = 1 },
                new BoneWeight1 { boneIndex = 1, weight = 1 },
                new BoneWeight1 { boneIndex = 0, weight = 1 },
            }, Allocator.Temp);
            mesh.SetBoneWeights(bpv, bws);

            mesh.bindposes = new Matrix4x4[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            // Run only the VisibleHeadAccessoryProcessor (not the full build pipeline
            // which strips PBBlocker)
            var maCtx = new nadena.dev.modular_avatar.core.editor.BuildContext(root);
            var processor = new VisibleHeadAccessoryProcessor(maCtx);
            processor.Process();

            var headClone = boneO1.parent.Find("Head (VHA Clone)");
            Assert.NotNull(headClone, "Head clone should be created");

            // PBBlocker should be present when running the VHA processor in isolation
            Assert.IsNull(boneO1.GetComponent<ModularAvatarPBBlocker>(),
                "VHA hierarchy bones should NOT have PBBlocker");
            Assert.NotNull(headClone.GetComponent<ModularAvatarPBBlocker>(),
                "Clone bones SHOULD have PBBlocker");
        }

        [Test]
        public void TestIndexFormatUpgradeToUInt32()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            // The VHA component is on boneO1, so _visibleBones contains boneO1
            // but NOT headBone. Thus bone index 1 (boneO1) = visible, bone index 0
            // (headBone) = non-visible.
            //
            // Layout: low indices = visible (boneO1), high indices = non-visible (headBone).
            // Clones of high-index non-visible vertices land at ~50000..~70000,
            // while visible vertices stay at 0..~30000, giving span > 65536.
            var visibleCount = 30000;  // vertices 0..29999, bone 1 = boneO1 = visible
            var nonVisibleCount = 20000; // vertices 30000..49999, bone 0 = headBone = non-visible
            var totalVertices = visibleCount + nonVisibleCount; // 50000

            var vertices = new Vector3[totalVertices];
            var bpv = new byte[totalVertices];
            var bws = new BoneWeight1[totalVertices];

            for (var i = 0; i < totalVertices; i++)
            {
                vertices[i] = new Vector3(i, 0, 0);
                bpv[i] = 1;
                // boneIndex 0 = headBone (non-visible), boneIndex 1 = boneO1 (visible)
                bws[i] = new BoneWeight1
                {
                    boneIndex = i < visibleCount ? 1 : 0,
                    weight = 1,
                };
            }

            // Triangles: (visible, non-visible, visible). Each non-visible vertex
            // appears in one mixed primitive → gets cloned, adding ~20000 clones
            // → total ~70000, span 69999 > 65535.
            var tris = new int[nonVisibleCount * 3];
            for (var i = 0; i < nonVisibleCount; i++)
            {
                var triBase = i * 3;
                tris[triBase] = i % visibleCount;           // visible
                tris[triBase + 1] = visibleCount + i;        // non-visible
                tris[triBase + 2] = (i + 1) % visibleCount;  // visible
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = tris;

            using var bpvNative = new NativeArray<byte>(bpv, Allocator.Temp);
            using var bwsNative = new NativeArray<BoneWeight1>(bws, Allocator.Temp);
            mesh.SetBoneWeights(bpvNative, bwsNative);

            mesh.bindposes = new[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            var processedMesh = smr.sharedMesh;
            Assert.AreNotSame(mesh, processedMesh);
            Assert.Greater(processedMesh.vertexCount, 65535);
            Assert.AreEqual(
                UnityEngine.Rendering.IndexFormat.UInt32,
                processedMesh.indexFormat
            );
        }

        [Test]
        public void TestUInt16WithBaseVertex()
        {
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");

            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            // 70000 vertices: V0-V39999 non-visible (bone 0 = headBone),
            // V40000-V69999 visible (bone 1 = boneO1).
            //
            // Submesh 0: V0-V29999, all non-visible → no mixed, no clones.
            // Submesh 1: V30000-V39999 (non-visible) + V40000-V49999 (visible)
            //   → mixed, V30000-V39999 clones to 70000-79999.
            //   Span: 40000..79999 = 39999 < 65536 → stays UInt16 w/ baseVertex=40000.
            // Total after VHA: 70000 + 10000 = 80000 > 65535.
            var nonVisCount = 40000;
            var visCount = 30000;
            var totalVertices = nonVisCount + visCount; // 70000

            var vertices = new Vector3[totalVertices];
            var bpv = new byte[totalVertices];
            var bws = new BoneWeight1[totalVertices];

            for (var i = 0; i < totalVertices; i++)
            {
                vertices[i] = new Vector3(i, 0, 0);
                bpv[i] = 1;
                bws[i] = new BoneWeight1
                {
                    boneIndex = i < nonVisCount ? 0 : 1,
                    weight = 1,
                };
            }

            // Submesh 0: 9999 triangles using V0-V29999 only (all non-visible)
            var sub0Tris = new int[29997];
            for (var i = 0; i < 9999; i++)
            {
                sub0Tris[i * 3] = i * 3;
                sub0Tris[i * 3 + 1] = i * 3 + 1;
                sub0Tris[i * 3 + 2] = i * 3 + 2;
            }

            // Submesh 1: mix V30000-V39999 (non-visible) + V40000-V49999 (visible)
            // Triangle pattern: (visible_i, non-visible_i, V40000) — no wraparound needed.
            var mixNonVisStart = 30000;
            var mixNonVisCount = 10000;
            var mixVisStart = 40000;
            var sub1Tris = new int[mixNonVisCount * 3];
            for (var i = 0; i < mixNonVisCount; i++)
            {
                var baseIdx = i * 3;
                sub1Tris[baseIdx] = mixVisStart + i;       // visible_i
                sub1Tris[baseIdx + 1] = mixNonVisStart + i; // non-visible_i
                sub1Tris[baseIdx + 2] = mixVisStart;         // V40000
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetIndices(sub0Tris, MeshTopology.Triangles, 0, false);
            mesh.SetIndices(sub1Tris, MeshTopology.Triangles, 1, false);

            using var bpvNative = new NativeArray<byte>(bpv, Allocator.Temp);
            using var bwsNative = new NativeArray<BoneWeight1>(bws, Allocator.Temp);
            mesh.SetBoneWeights(bpvNative, bwsNative);

            mesh.bindposes = new[]
            {
                headBone.worldToLocalMatrix,
                boneO1.worldToLocalMatrix,
            };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            var processedMesh = smr.sharedMesh;
            Assert.AreNotSame(mesh, processedMesh);
            Assert.Greater(processedMesh.vertexCount, 65535);
            Assert.AreEqual(
                UnityEngine.Rendering.IndexFormat.UInt16,
                processedMesh.indexFormat
            );

            // Submesh 0: no clones → indices unchanged, baseVertex=0
            Assert.AreEqual(0, processedMesh.GetBaseVertex(0));
            var sub0Stored = processedMesh.GetIndices(0);
            Assert.AreEqual(sub0Tris.Length, sub0Stored.Length);
            for (var i = 0; i < 30; i++)
                Assert.AreEqual(sub0Tris[i], sub0Stored[i]);

            // Submesh 1: baseVertex should be 40000 (min index in this submesh)
            var baseV = processedMesh.GetBaseVertex(1);
            Assert.AreEqual(mixVisStart, baseV);

            var sub1Stored = processedMesh.GetIndices(1);
            Assert.AreEqual(sub1Tris.Length, sub1Stored.Length);

            // GetIndices returns absolute vertex indices (baseVertex already applied).
            // First triangle: V40000 stays, V30000 → clone at 70000, third vertex is always V40000.
            Assert.AreEqual(40000, sub1Stored[0], "V40000 stays visible");
            Assert.AreEqual(70000, sub1Stored[1], "Clone of V30000");
            Assert.AreEqual(40000, sub1Stored[2], "Third vertex is V40000");
            // Last triangle (i=9999): V49999 stays, V39999 → clone at 79999, third vertex is V40000.
            var lastTri = mixNonVisCount - 1;
            Assert.AreEqual(mixVisStart + lastTri, sub1Stored[lastTri * 3],
                $"V{mixVisStart + lastTri} stays visible");
            Assert.AreEqual(
                70000 + lastTri,
                sub1Stored[lastTri * 3 + 1],
                "Clone of V" + (mixNonVisStart + lastTri)
            );
            Assert.AreEqual(40000, sub1Stored[lastTri * 3 + 2], "Third vertex is V40000");
        }

        [Test]
        public void TestBindposeMathForClonedBones()
        {
            // Verifies the skinning math identity for cloned bones:
            //   clone.localToWorld * clone_bindpose * V_local
            //     == original.localToWorld * original_bindpose * V_local
            // which is required for cloned vertices to render at the same world
            // position as their originals.
            //
            // The fix achieves this by (a) parenting the clone under the original
            // with identity local transform, and (b) preserving the original
            // bone's bindpose in the clone's slot.
            var root = CreateCommonPrefab("ShapellAvatarVRC.prefab");
            var animator = root.GetComponent<Animator>();
            var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.NotNull(headBone);

            // Non-trivial bone offset/rotation so the assertion catches a missing
            // identity-local-transform on the clone.
            var boneO1Go = CreateChild(headBone.gameObject, "BoneO1");
            var boneO1 = boneO1Go.transform;
            boneO1.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            boneO1.localRotation = Quaternion.Euler(15, 30, 45);
            boneO1Go.AddComponent<ModularAvatarVisibleHeadAccessory>();

            var smrGo = new GameObject("Accessory Mesh");
            smrGo.transform.SetParent(boneO1, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();

            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0.5f, 1.0f, 1.5f),
                new Vector3(-0.5f, 0.5f, 0.0f),
                new Vector3(0.0f, 0.0f, 0.0f),
            };
            mesh.triangles = new int[] { 0, 1, 2 };

            using var bpv = new NativeArray<byte>(new byte[] { 1, 1, 1 }, Allocator.Temp);
            using var bws = new NativeArray<BoneWeight1>(new BoneWeight1[]
            {
                new BoneWeight1 { boneIndex = 0, weight = 1 },
                new BoneWeight1 { boneIndex = 1, weight = 1 },
                new BoneWeight1 { boneIndex = 1, weight = 1 },
            }, Allocator.Temp);
            mesh.SetBoneWeights(bpv, bws);

            var bindpose0 = headBone.worldToLocalMatrix;
            var bindpose1 = boneO1.worldToLocalMatrix;
            mesh.bindposes = new Matrix4x4[] { bindpose0, bindpose1 };
            mesh.RecalculateBounds();
            TrackObject(mesh);

            smr.sharedMesh = mesh;
            smr.bones = new[] { headBone, boneO1 };

            Assert.DoesNotThrow(() => AvatarProcessor.ProcessAvatar(root));

            var processedMesh = smr.sharedMesh;
            var newBones = smr.bones;
            var newBindposes = processedMesh.bindposes;

            Transform cloneBone = null;
            int cloneBoneIndex = -1;
            for (var i = 0; i < newBones.Length; i++)
            {
                if (newBones[i] != headBone && newBones[i] != boneO1)
                {
                    cloneBone = newBones[i];
                    cloneBoneIndex = i;
                    break;
                }
            }
            Assert.NotNull(cloneBone, "Clone bone should exist");
            Assert.AreEqual(headBone, cloneBone.parent, "Clone should be parented under the original bone");

            const float tol = 1e-4f;
            Assert.AreEqual(0f, cloneBone.localPosition.x, tol, "Clone local x");
            Assert.AreEqual(0f, cloneBone.localPosition.y, tol, "Clone local y");
            Assert.AreEqual(0f, cloneBone.localPosition.z, tol, "Clone local z");
            Assert.AreEqual(0f, cloneBone.localRotation.x, tol, "Clone local rot x");
            Assert.AreEqual(0f, cloneBone.localRotation.y, tol, "Clone local rot y");
            Assert.AreEqual(0f, cloneBone.localRotation.z, tol, "Clone local rot z");
            Assert.AreEqual(1f, cloneBone.localRotation.w, tol, "Clone local rot w");

            // Clone's bindpose must equal original bone's bindpose (RemapBone preserves it).
            AssertMatricesEqual(bindpose0, newBindposes[cloneBoneIndex], tol);
        }

        private static void AssertMatricesEqual(Matrix4x4 expected, Matrix4x4 actual, float tol)
        {
            Assert.AreEqual(expected.m00, actual.m00, tol, "m00");
            Assert.AreEqual(expected.m01, actual.m01, tol, "m01");
            Assert.AreEqual(expected.m02, actual.m02, tol, "m02");
            Assert.AreEqual(expected.m03, actual.m03, tol, "m03");
            Assert.AreEqual(expected.m10, actual.m10, tol, "m10");
            Assert.AreEqual(expected.m11, actual.m11, tol, "m11");
            Assert.AreEqual(expected.m12, actual.m12, tol, "m12");
            Assert.AreEqual(expected.m13, actual.m13, tol, "m13");
            Assert.AreEqual(expected.m20, actual.m20, tol, "m20");
            Assert.AreEqual(expected.m21, actual.m21, tol, "m21");
            Assert.AreEqual(expected.m22, actual.m22, tol, "m22");
            Assert.AreEqual(expected.m23, actual.m23, tol, "m23");
            Assert.AreEqual(expected.m30, actual.m30, tol, "m30");
            Assert.AreEqual(expected.m31, actual.m31, tol, "m31");
            Assert.AreEqual(expected.m32, actual.m32, tol, "m32");
            Assert.AreEqual(expected.m33, actual.m33, tol, "m33");
        }
    }
}

#endif