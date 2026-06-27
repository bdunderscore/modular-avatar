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
    }
}

#endif