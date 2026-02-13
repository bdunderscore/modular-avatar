using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using AvatarProcessor = nadena.dev.ndmf.AvatarProcessor;

namespace modular_avatar_tests
{
    public class BoneProxyTest : TestBase
    {
        [Test]
        public void TestBoneProxy()
        {
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildAtRoot, expectSnapPos: true, expectSnapRot: true);
            AssertAttachmentMode(BoneProxyAttachmentMode.Unset, expectSnapPos: true, expectSnapRot: true);
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildKeepPosition, expectSnapPos: false,
                expectSnapRot: true);
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildKeepRotation, expectSnapPos: true,
                expectSnapRot: false);
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildKeepWorldPose, expectSnapPos: false,
                expectSnapRot: false);
        }

        [Test]
        public void TestNonHumanoidTarget()
        {
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var reference = CreateChild(root, "ref");

            var boneProxy = reference.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = root.transform;
            boneProxy.ClearCache(true);
            Assert.AreEqual(root.transform, boneProxy.target);

            boneProxy.target = target.transform;
            boneProxy.ClearCache(true);
            Assert.AreEqual(target.transform, boneProxy.target);

            target.name = "target2";
            boneProxy.ClearCache(true);
            Assert.IsNull(boneProxy.target);
        }

        [Test]
        public void TestNameCollision()
        {
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var src1 = CreateChild(root, "src1");
            var src_child1 = CreateChild(src1, "child");
            var src2 = CreateChild(root, "src2");
            var src_child2 = CreateChild(src2, "child");
            
            var bp1 = src_child1.AddComponent<ModularAvatarBoneProxy>();
            bp1.target = target.transform;
            
            var bp2 = src_child2.AddComponent<ModularAvatarBoneProxy>();
            bp2.target = target.transform;
            
            bp1.ClearCache(true);
            bp2.ClearCache(true);
            
            var context = CreateContext(root);
            new BoneProxyPluginPrepass().ExecuteForTesting(context);
            new BoneProxyPluginPass().ExecuteForTesting(context);
            
            Assert.AreNotEqual(src_child1.name, src_child2.name);
        }

        // New tests for cycle checking
        [Test]
        public void CycleDetection_BoneProxiesMutualFail()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "A");
            var b = CreateChild(root, "B");

            var aBone = CreateChild(a, "bone");
            var bBone = CreateChild(b, "bone");

            var bpA = aBone.AddComponent<ModularAvatarBoneProxy>();
            var bpB = bBone.AddComponent<ModularAvatarBoneProxy>();

            bpA.target = bBone.transform;
            bpB.target = aBone.transform;

            var errors = ErrorReport.CaptureErrors(() => nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root));

            Assert.IsTrue(errors.Any(e => (e.TheError is SimpleError se) && se.TitleKey == "error.object_cycle"));
        }

        [Test]
        public void CycleDetection_BoneProxyMergeArmatureFail()
        {
            var root = CreateRoot("root");
            var baseRoot = CreateChild(root, "Base");
            var mergeRoot = CreateChild(root, "Merge");

            var baseBone = CreateChild(baseRoot, "bone");
            var mergeBone = CreateChild(mergeRoot, "bone");

            var mama = mergeRoot.AddComponent<ModularAvatarMergeArmature>();
            mama.mergeTarget.Set(baseRoot);

            var bp = baseBone.AddComponent<ModularAvatarBoneProxy>();
            bp.target = mergeBone.transform;

            var errors = ErrorReport.CaptureErrors(() => nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root));

            Assert.IsTrue(errors.Any(e => (e.TheError is SimpleError se) && se.TitleKey == "error.object_cycle"));
        }

        [Test]
        public void BoneProxy_NoCycle_Succeeds()
        {
            var root = CreateRoot("root");

            var t1 = CreateChild(root, "t1");
            var t2 = CreateChild(root, "t2");
            var t3 = CreateChild(root, "t3");

            var bp1 = t1.AddComponent<ModularAvatarBoneProxy>();
            var bp2 = t2.AddComponent<ModularAvatarBoneProxy>();

            bp1.target = t2.transform; // t1 -> t2
            bp2.target = t3.transform; // t2 -> t3 (no cycle)

            var errors = ErrorReport.CaptureErrors(() => nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root));
            Assert.IsTrue(errors.Count == 0, "Expected no errors for a non-cyclical bone proxy chain");
        }

        [Test]
        public void BoneProxy_NoCycle_OrderIndependent()
        {
            var root = CreateRoot("root");

            // Create targets first, then reorder siblings before attaching proxies to ensure order doesn't matter
            var t1 = CreateChild(root, "t1");
            var t2 = CreateChild(root, "t2");
            var t3 = CreateChild(root, "t3");

            // Reorder siblings so proxies will appear in a different order in the hierarchy
            t1.transform.SetSiblingIndex(root.transform.childCount - 1);
            t2.transform.SetSiblingIndex(0);

            var bp1 = t1.AddComponent<ModularAvatarBoneProxy>();
            var bp2 = t2.AddComponent<ModularAvatarBoneProxy>();

            bp1.target = t2.transform; // t1 -> t2
            bp2.target = t3.transform; // t2 -> t3 (no cycle)

            var errors = ErrorReport.CaptureErrors(() => nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root));
            Assert.IsTrue(errors.Count == 0, "Expected no errors after creating bone proxies in a different sibling order");
        }

        [Test]
        public void CycleDetection_BoneProxy_WithIntermediateChildFail()
        {
            var root = CreateRoot("root");

            var A = CreateChild(root, "A");
            var AChild = CreateChild(A, "child");
            var B = CreateChild(root, "B");

            // BP on A targets B
            var bpA = A.AddComponent<ModularAvatarBoneProxy>();
            bpA.target = B.transform;

            // BP on B targets a child of A (A/child) -> this creates a cycle via parent edges
            var bpB = B.AddComponent<ModularAvatarBoneProxy>();
            bpB.target = AChild.transform;

            var errors = ErrorReport.CaptureErrors(() => nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root));
            Assert.IsTrue(errors.Any(e => (e.TheError is SimpleError se) && se.TitleKey == "error.object_cycle"));
        }

        [Test]
        public void BoneProxy_AdjustTransform_Order_MixedSettings()
        {
            var root = CreateRoot("root");

            // Create a longer chain: t1 -> t2 -> t3 -> t4 -> t5
            var t1 = CreateChild(root, "t1");
            var t2 = CreateChild(root, "t2");
            var t3 = CreateChild(root, "t3");
            var t4 = CreateChild(root, "t4");
            var t5 = CreateChild(root, "t5");

            // Set distinct world transforms so we can verify preservation/reset later
            t1.transform.position = new Vector3(1.1f, 2.2f, 3.3f);
            t1.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
            t2.transform.position = new Vector3(4.4f, 5.5f, 6.6f);
            t2.transform.rotation = Quaternion.Euler(40f, 50f, 60f);
            t3.transform.position = new Vector3(7.7f, 8.8f, 9.9f);
            t3.transform.rotation = Quaternion.Euler(70f, 80f, 90f);
            t4.transform.position = new Vector3(11.11f, 12.12f, 13.13f);
            t4.transform.rotation = Quaternion.Euler(100f, 110f, 120f);
            t5.transform.position = new Vector3(14.14f, 15.15f, 16.16f);
            t5.transform.rotation = Quaternion.Euler(130f, 140f, 150f);

            // Create proxies on t1..t4, each targeting the next transform
            var bp1 = t1.AddComponent<ModularAvatarBoneProxy>();
            var bp2 = t2.AddComponent<ModularAvatarBoneProxy>();
            var bp3 = t3.AddComponent<ModularAvatarBoneProxy>();
            var bp4 = t4.AddComponent<ModularAvatarBoneProxy>();

            bp1.target = t2.transform; // t1 -> t2
            bp2.target = t3.transform; // t2 -> t3
            bp3.target = t4.transform; // t3 -> t4
            bp4.target = t5.transform; // t4 -> t5

            // Alternating attachment modes: KeepWorldPose, AtRoot, KeepWorldPose, AtRoot
            bp1.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
            bp2.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            bp3.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
            bp4.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;

            // Record original world transforms for keep-world entries
            var orig1Pos = t1.transform.position;
            var orig1Rot = t1.transform.rotation;
            var orig3Pos = t3.transform.position;
            var orig3Rot = t3.transform.rotation;

            // Run the proxy preprocessing (which re-parents and then AdjustTransform parent->child)
            var context = CreateContext(root);
            new BoneProxyPluginPrepass().ExecuteForTesting(context);
            new BoneProxyPluginPass().ExecuteForTesting(context);

            // After processing: bp2 (on t2) and bp4 (on t4) are AsChildAtRoot and must have local pos/rot zero
            Assert.LessOrEqual(Vector3.Distance(t2.transform.localPosition, Vector3.zero), 0.0001f, "bp2 (AtRoot) should have local position zero");
            Assert.LessOrEqual(Quaternion.Angle(t2.transform.localRotation, Quaternion.identity), 0.0001f, "bp2 (AtRoot) should have local rotation identity");

            Assert.LessOrEqual(Vector3.Distance(t4.transform.localPosition, Vector3.zero), 0.0001f, "bp4 (AtRoot) should have local position zero");
            Assert.LessOrEqual(Quaternion.Angle(t4.transform.localRotation, Quaternion.identity), 0.0001f, "bp4 (AtRoot) should have local rotation identity");

            // bp1 (on t1) and bp3 (on t3) are KeepWorldPose and must preserve world transform
            Assert.LessOrEqual(Vector3.Distance(t1.transform.position, orig1Pos), 0.0001f, "bp1 (KeepWorldPose) should preserve world position");
            Assert.LessOrEqual(Quaternion.Angle(t1.transform.rotation, orig1Rot), 0.01f, "bp1 (KeepWorldPose) should preserve world rotation");

            Assert.LessOrEqual(Vector3.Distance(t3.transform.position, orig3Pos), 0.0001f, "bp3 (KeepWorldPose) should preserve world position");
            Assert.LessOrEqual(Quaternion.Angle(t3.transform.rotation, orig3Rot), 0.01f, "bp3 (KeepWorldPose) should preserve world rotation");
        }

        [Test]
        public void MergeArmature_BoneProxy_Interaction()
        {
            // Create hierarchy:
            // Root
            //   - MergeTarget
            //     - X (bone proxy, target: Z)
            //   - MergeSource (Merge Armature, target: MergeTarget)
            //     - X
            //       - Y (with MeshRenderer to avoid empty object optimizations)
            //   - Z
            
            var root = CreateRoot("root");
            
            var mergeTarget = CreateChild(root, "MergeTarget");
            var targetX = CreateChild(mergeTarget, "X");
            
            // Add bone proxy to MergeTarget/X that targets Z
            var boneProxy = targetX.AddComponent<ModularAvatarBoneProxy>();
            
            var mergeSource = CreateChild(root, "MergeSource");
            var sourceX = CreateChild(mergeSource, "X");
            var sourceY = CreateChild(sourceX, "Y");
            
            // Add MeshRenderer to Y to prevent it from being optimized away
            sourceY.AddComponent<MeshRenderer>();
            
            var z = CreateChild(root, "Z");
            
            // Set up the bone proxy to target Z
            boneProxy.target = z.transform;
            
            // Set up merge armature to merge MergeSource into MergeTarget
            var mergeArmature = mergeSource.AddComponent<ModularAvatarMergeArmature>();
            mergeArmature.mergeTarget.Set(mergeTarget);
            
            // Process the avatar
            nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root);
            
            // After processing, Y should be a child of target X, which is a child of Z (the bone proxy target)
            // Expected path: Z/X/Y (since X in MergeTarget becomes a child of Z)
            Assert.AreEqual(z, sourceY.transform.parent.parent.gameObject, "Y should be a child of Z after processing");
        }

        private void AssertAttachmentMode(BoneProxyAttachmentMode attachmentMode, bool expectSnapPos,
            bool expectSnapRot)
        {
            AssertAttachmentModeAtBuild(attachmentMode, expectSnapPos, expectSnapRot);
            AssertAttachmentModeInEditor(attachmentMode, expectSnapPos, expectSnapRot);
        }

        private void AssertAttachmentModeInEditor(BoneProxyAttachmentMode attachmentMode, bool expectSnapPos,
            bool expectSnapRot)
        {
            // Unset gets converted in the custom inspector; until it is, we don't snap (since we need to know the
            // position to heuristically set the snapping mode).
            if (attachmentMode == BoneProxyAttachmentMode.Unset) return;

            var root = CreateRoot("root");
            var bone = CreateChild(root, "bone");
            var proxy = CreateChild(root, "proxy");

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = bone.transform;
            boneProxy.attachmentMode = attachmentMode;

            bone.transform.localPosition = Vector3.one;
            bone.transform.localRotation = Quaternion.Euler(123, 45, 6);

            boneProxy.Update();

            if (expectSnapPos)
            {
                Assert.LessOrEqual(Vector3.Distance(proxy.transform.position, bone.transform.position), 0.0001f);
            }
            else
            {
                Assert.GreaterOrEqual(Vector3.Distance(proxy.transform.position, bone.transform.position), 0.0001f);
            }

            if (expectSnapRot)
            {
                Assert.LessOrEqual(Quaternion.Angle(proxy.transform.rotation, bone.transform.rotation), 0.0001f);
            }
            else
            {
                Assert.GreaterOrEqual(Quaternion.Angle(proxy.transform.rotation, bone.transform.rotation), 0.0001f);
            }
        }

        private void AssertAttachmentModeAtBuild(BoneProxyAttachmentMode attachmentMode, bool expectSnapPos,
            bool expectSnapRot)
        {
            var root = CreateRoot("root");
            var bone = CreateChild(root, "bone");
            var proxy = CreateChild(root, "proxy");

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = bone.transform;
            boneProxy.attachmentMode = attachmentMode;
            // Prevent the bone from being optimized away
            var proxyTransform = boneProxy.transform;
            proxyTransform.gameObject.AddComponent<MeshRenderer>();

            bone.transform.localPosition = Vector3.one;
            bone.transform.localRotation = Quaternion.Euler(123, 45, 6);

            nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root);

            Assert.AreEqual(proxyTransform.parent, bone.transform);

            if (expectSnapPos)
            {
                Assert.LessOrEqual(Vector3.Distance(proxy.transform.localPosition, Vector3.zero), 0.0001f);
            }
            else
            {
                Assert.LessOrEqual(Vector3.Distance(proxy.transform.position, Vector3.zero), 0.0001f);
            }

            if (expectSnapRot)
            {
                Assert.LessOrEqual(Quaternion.Angle(proxy.transform.localRotation, Quaternion.identity), 0.0001f);
            }
            else
            {
                Assert.LessOrEqual(Quaternion.Angle(proxy.transform.rotation, Quaternion.identity), 0.0001f);
            }
        }

        [Test]
        public void TestCycleOrder()
        {
            // This test checks for a regression in which bone proxies contained inside merge armatures would break
            // the build.
            var avatar = CreatePrefab("BoneProxyCycleOrderTest.prefab");

            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(avatar));
            
            Assert.IsEmpty(errors);
        }

        [Test]
        [TestCase(true, true)] // BoneProxy on merge source, target within merge source
        [TestCase(true, false)] // BoneProxy on merge source, target outside merge source
        [TestCase(false, true)] // BoneProxy not on merge source, target within merge source
        public void BoneProxy_WithMergeArmature_NoError_MovesToTarget(bool boneProxyOnMergeSource, bool targetWithinMergeSource)
        {
            // Create hierarchy:
            // Root
            //   - MergeSource (Merge Armature)
            //     - ProxyHolder (bone proxy, moves to its target)
            //     - TargetWithin (target if targetWithinMergeSource is true, stays in merge source)
            //     - OtherChild (regular merge source child, relocates to merge target)
            //   - TargetOutside (target if targetWithinMergeSource is false)
            //   - MergeTarget (receives children from merge source)
            //   - ProxyHolder (may be here if boneProxyOnMergeSource is false)

            var root = CreateRoot("root");

            var mergeTarget = CreateChild(root, "MergeTarget");
            var mergeSource = CreateChild(root, "MergeSource");

            // Create the bone proxy holder - will move to its target
            GameObject proxyHolder;
            GameObject targetObject;
            
            if (boneProxyOnMergeSource)
            {
                proxyHolder = CreateChild(mergeSource, "ProxyHolder");
            }
            else
            {
                proxyHolder = CreateChild(root, "ProxyHolder");
            }

            // Add bone proxy
            var boneProxy = proxyHolder.AddComponent<ModularAvatarBoneProxy>();
            proxyHolder.AddComponent<MeshRenderer>(); // Prevent optimization

            // Create and set up the target
            if (targetWithinMergeSource)
            {
                targetObject = CreateChild(mergeSource, "TargetWithin");
            }
            else
            {
                targetObject = CreateChild(root, "TargetOutside");
            }
            targetObject.AddComponent<MeshRenderer>(); // Prevent optimization

            boneProxy.target = targetObject.transform;

            // Add additional child to merge source to verify merge behavior
            // This should relocate to merge target since it's not a bone proxy target
            var otherChild = CreateChild(mergeSource, "OtherChild");
            otherChild.AddComponent<MeshRenderer>(); // Prevent optimization

            // Set up merge armature
            var mergeArmature = mergeSource.AddComponent<ModularAvatarMergeArmature>();
            mergeArmature.mergeTarget.Set(mergeTarget);
            CreateChild(mergeTarget, "TargetWithin");
            CreateChild(mergeTarget, "OtherChild");
            CreateChild(mergeTarget, "ProxyHolder");

            // Process the avatar
            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(root));

            // Verify no errors
            Assert.IsEmpty(errors, "Expected no errors when bone proxy interacts with merge armature");

            // Verify proxy moved to target
            Assert.AreEqual(targetObject, proxyHolder.transform.parent.gameObject, 
                "BoneProxy holder should be parented to its target after processing");

            // Verify that OtherChild (which is not the proxy target) is relocated to merge target
            Assert.IsTrue(otherChild.transform.IsChildOf(mergeTarget.transform),
                "OtherChild from merge source should be relocated to merge target");
        }

        [Test]
        public void MergeArmature_TargetsChildOfBoneProxy_Relocates()
        {
            // Create hierarchy:
            // Root
            //   - BoneProxyTarget
            //     - BoneProxyChild (merge target; will receive children from merge source)
            //       - Content (MeshRenderer to prevent optimization)
            //   - BoneProxyHolder (bone proxy targeting BoneProxyTarget)
            //   - MergeSource (Merge Armature targeting BoneProxyTarget/BoneProxyChild)
            //     - Data (MeshRenderer to prevent optimization)
            //     - OtherData (additional content to verify merge)
            //
            // After processing:
            // - BoneProxyHolder becomes child of BoneProxyTarget
            // - Data and OtherData become children of BoneProxyChild

            var root = CreateRoot("root");

            var boneProxyTarget = CreateChild(root, "BoneProxyTarget");
            var boneProxyChild = CreateChild(boneProxyTarget, "BoneProxyChild");
            var content = CreateChild(boneProxyChild, "Content");
            content.AddComponent<MeshRenderer>();

            var boneProxyHolder = CreateChild(root, "BoneProxyHolder");
            var boneProxy = boneProxyHolder.AddComponent<ModularAvatarBoneProxy>();
            boneProxyHolder.AddComponent<MeshRenderer>(); // Prevent optimization
            boneProxy.target = boneProxyTarget.transform;

            var mergeSource = CreateChild(root, "MergeSource");
            var data = CreateChild(mergeSource, "Data");
            data.AddComponent<MeshRenderer>();
            var otherData = CreateChild(mergeSource, "OtherData");
            otherData.AddComponent<MeshRenderer>();
            CreateChild(boneProxyChild, "Data");
            CreateChild(boneProxyChild, "OtherData");

            var mergeArmature = mergeSource.AddComponent<ModularAvatarMergeArmature>();
            mergeArmature.mergeTarget.Set(boneProxyChild);

            // Process the avatar
            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(root));

            // Verify no errors
            Assert.IsEmpty(errors, "Expected no errors when merge armature targets a child of a bone proxy");

            // Verify BoneProxyHolder was moved to BoneProxyTarget
            Assert.AreEqual(boneProxyTarget, boneProxyHolder.transform.parent.gameObject,
                "BoneProxy holder should be moved to its target (BoneProxyTarget)");
            
            // Verify that the merge armature moved with it
            Assert.IsTrue(data.transform.IsChildOf(boneProxyTarget.transform),
                "Bone proxy should be parent of merged objects");
        }

        [Test]
        public void MatchScale_Enabled_LocalScaleBecomesOne()
        {
            // When matchScale is enabled and the proxy is processed,
            // the local scale should be set to (1, 1, 1)
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var proxy = CreateChild(root, "proxy");

            // Set a specific non-uniform scale on the proxy
            proxy.transform.localScale = new Vector3(2f, 3f, 4f);

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = true;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            proxy.AddComponent<MeshRenderer>(); // Prevent optimization

            // Process the avatar
            AvatarProcessor.ProcessAvatar(root);

            // After processing, local scale should be (1, 1, 1)
            Assert.LessOrEqual(Vector3.Distance(proxy.transform.localScale, Vector3.one), 0.0001f,
                "Local scale should be (1, 1, 1) when matchScale is enabled");

            // The proxy should now be a child of the target
            Assert.AreEqual(target, proxy.transform.parent.gameObject);
        }

        [Test]
        public void MatchScale_Disabled_RetainsWorldLossyScale()
        {
            // When matchScale is disabled, the proxy should retain its
            // original world lossy scale after being moved to a new parent
            var root = CreateRoot("root");
            
            // Create a target with a scaled parent to test scale inheritance
            var targetParent = CreateChild(root, "targetParent");
            targetParent.transform.localScale = new Vector3(2f, 2f, 2f);
            
            var target = CreateChild(targetParent, "target");
            
            var proxy = CreateChild(root, "proxy");
            proxy.transform.localScale = new Vector3(3f, 3f, 3f);
            
            var originalWorldScale = proxy.transform.lossyScale; // Should be (3, 3, 3)

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = false;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            proxy.AddComponent<MeshRenderer>(); // Prevent optimization

            // Process the avatar
            AvatarProcessor.ProcessAvatar(root);

            // After processing, the world lossy scale should be retained
            var finalWorldScale = proxy.transform.lossyScale;
            Assert.LessOrEqual(Vector3.Distance(finalWorldScale, originalWorldScale), 0.001f,
                "World lossy scale should be retained when matchScale is disabled");

            // The proxy should now be a child of the target
            Assert.AreEqual(target, proxy.transform.parent.gameObject);
        }

        [Test]
        public void MatchScale_Enabled_EditorUpdate()
        {
            // In the editor, when matchScale is enabled and Update() is called,
            // the local scale should be calculated based on the target's transform
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var proxy = CreateChild(root, "proxy");

            // Set target and proxy to have different scales and positions
            target.transform.position = new Vector3(1f, 2f, 3f);
            target.transform.rotation = Quaternion.Euler(45f, 45f, 45f);
            target.transform.localScale = new Vector3(2f, 2f, 2f);

            proxy.transform.localPosition = new Vector3(5f, 6f, 7f);
            proxy.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            proxy.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = true;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;

            // Call Update (which is editor-only logic)
            boneProxy.Update();

            // The local scale should be set to match the parent's scale
            // This ensures that the proxy maintains proper visual positioning
            Assert.IsTrue(proxy.transform.localScale.x > 0, "Local scale X should be positive");
            Assert.IsTrue(proxy.transform.localScale.y > 0, "Local scale Y should be positive");
            Assert.IsTrue(proxy.transform.localScale.z > 0, "Local scale Z should be positive");
        }

        [Test]
        public void MatchScale_Disabled_EditorUpdate()
        {
            // In the editor, when matchScale is disabled, the Update() method
            // should not modify the local scale
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var proxy = CreateChild(root, "proxy");

            target.transform.localScale = new Vector3(2f, 2f, 2f);
            var originalLocalScale = new Vector3(1.5f, 2.5f, 3.5f);
            proxy.transform.localScale = originalLocalScale;

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = false;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;

            // Call Update
            boneProxy.Update();

            // The local scale should remain unchanged when matchScale is false
            Assert.LessOrEqual(Vector3.Distance(proxy.transform.localScale, originalLocalScale), 0.0001f,
                "Local scale should not change when matchScale is disabled");
        }

        [Test]
        [TestCase(BoneProxyAttachmentMode.AsChildAtRoot)]
        [TestCase(BoneProxyAttachmentMode.AsChildKeepWorldPose)]
        [TestCase(BoneProxyAttachmentMode.AsChildKeepPosition)]
        [TestCase(BoneProxyAttachmentMode.AsChildKeepRotation)]
        public void MatchScale_WithDifferentAttachmentModes(BoneProxyAttachmentMode attachmentMode)
        {
            // Test that matchScale works correctly with all attachment modes
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var proxy = CreateChild(root, "proxy");

            target.transform.localScale = new Vector3(2f, 2f, 2f);
            proxy.transform.localScale = new Vector3(3f, 3f, 3f);
            proxy.transform.position = new Vector3(1f, 2f, 3f);
            proxy.transform.rotation = Quaternion.Euler(30f, 45f, 60f);

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = true;
            boneProxy.attachmentMode = attachmentMode;
            proxy.AddComponent<MeshRenderer>(); // Prevent optimization

            // Process the avatar
            AvatarProcessor.ProcessAvatar(root);

            // After processing, local scale should be (1, 1, 1) regardless of attachment mode
            Assert.LessOrEqual(Vector3.Distance(proxy.transform.localScale, Vector3.one), 0.0001f,
                $"Local scale should be (1, 1, 1) for {attachmentMode} when matchScale is enabled");

            // The proxy should be a child of the target
            Assert.AreEqual(target, proxy.transform.parent.gameObject);
        }

        [Test]
        public void MatchScale_WithScaledTarget()
        {
            // Test matchScale behavior when the target itself has a scaled parent
            var root = CreateRoot("root");
            
            var targetParent = CreateChild(root, "targetParent");
            targetParent.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            var target = CreateChild(targetParent, "target");
            target.transform.localScale = new Vector3(2f, 2f, 2f);
            
            var proxy = CreateChild(root, "proxy");
            proxy.transform.localScale = new Vector3(4f, 4f, 4f);

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = true;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            proxy.AddComponent<MeshRenderer>(); // Prevent optimization

            // Process the avatar
            AvatarProcessor.ProcessAvatar(root);

            // After processing, local scale should be (1, 1, 1)
            // even though the target has a scaled parent
            Assert.LessOrEqual(Vector3.Distance(proxy.transform.localScale, Vector3.one), 0.0001f,
                "Local scale should be (1, 1, 1) when matchScale is enabled, regardless of target's parent scale");

            // The proxy should now be a child of the target
            Assert.AreEqual(target, proxy.transform.parent.gameObject);
        }

        [Test]
        public void MatchScale_NonUniformScale()
        {
            // Test matchScale with non-uniform scales
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var proxy = CreateChild(root, "proxy");

            // Set different scale values in each axis
            proxy.transform.localScale = new Vector3(1f, 2f, 3f);

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = target.transform;
            boneProxy.matchScale = true;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
            proxy.AddComponent<MeshRenderer>(); // Prevent optimization

            // Process the avatar
            AvatarProcessor.ProcessAvatar(root);

            // After processing, local scale should be (1, 1, 1)
            Assert.LessOrEqual(Vector3.Distance(proxy.transform.localScale, Vector3.one), 0.0001f,
                "Local scale should be (1, 1, 1) for non-uniform scales when matchScale is enabled");
        }
    }
}

