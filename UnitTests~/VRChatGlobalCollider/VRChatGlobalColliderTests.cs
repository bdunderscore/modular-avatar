#if MA_VRCSDK3_AVATARS
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.VRChatGlobalCollider
{
    public class VRChatGlobalColliderTests : TestBase
    {
        [Test]
        public void AutoRemapUpdatesDescriptor()
        {
            // Load preconfigured humanoid avatar prefab
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Add a single auto-mode global collider to the avatar root
            var colliderObj = CreateChild(root, "MyCollider");
            var comp = colliderObj.AddComponent<ModularAvatarGlobalCollider>();
            comp.ManualRemap = false; // auto mode
            comp.Radius = 0.123f;
            comp.Height = 0.456f;
            comp.Position = new Vector3(1f, 2f, 3f);
            comp.Rotation = Quaternion.Euler(10f, 20f, 30f);

            // Run the pass (AnimatorServicesContext is not required for this pass)
            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            // Verify descriptor updates (auto remap picks FingerRingLeft first)
            var desc = root.GetComponent<VRCAvatarDescriptor>();
            Assert.NotNull(desc, "Avatar prefab must have a VRCAvatarDescriptor");

            // The pass should set the collider to custom and apply radius/height
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingL.state);
            Assert.AreEqual(0.123f, desc.collider_fingerRingL.radius, 1e-6f);
            Assert.AreEqual(0.456f, desc.collider_fingerRingL.height, 1e-6f);
            Assert.IsFalse(desc.collider_fingerRingL.isMirrored);
            Assert.NotNull(desc.collider_fingerRingL.transform);

            // Verify all other colliders remain in Automatic state
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_head.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_torso.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_handL.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_handR.state);

            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerRingR.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerMiddleL.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerMiddleR.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerLittleL.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerLittleR.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerIndexL.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_fingerIndexR.state);

            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_footL.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_footR.state);

            // The pass creates MA_ColliderRoot and MA_ColliderTarget for finger colliders. Verify they were created
            var foundRoot = colliderObj.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("MA_ColliderRoot_FingerRingLeft_"));
            var foundTarget = colliderObj.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("MA_ColliderTarget_FingerRingLeft_"));

            Assert.NotNull(foundRoot, "Collider root object should be created for finger collider");
            Assert.NotNull(foundTarget, "Collider target object should be created for finger collider");

            // Verify hierarchy: target is a child of root
            Assert.AreEqual(foundRoot, foundTarget.parent);

            // ColliderRoot.localPosition == (my.Position.x, (my.Height * -0.5f) + my.Position.y, my.Position.z)
            var expectedRootLocalPos = new Vector3(comp.Position.x, (comp.Height * -0.5f) + comp.Position.y, comp.Position.z);
            Assert.AreEqual(expectedRootLocalPos, foundRoot.localPosition);

            // Compare rotations approximately
            Assert.Less(Quaternion.Angle(comp.Rotation, foundRoot.localRotation), 0.001f);

            // ColliderTarget.localPosition == (0, 0.001f, 0)
            Assert.AreEqual(new Vector3(0f, 0.001f, 0f), foundTarget.localPosition);

            // The descriptor collider transform should be the ColliderTarget transform
            Assert.AreEqual(foundTarget, desc.collider_fingerRingL.transform);

            // The component should be removed from the hierarchy
            Assert.AreEqual(0, root.GetComponentsInChildren<ModularAvatarGlobalCollider>(true).Length);
        }

        [Test]
        public void ManualNonFingerRemapDoesNotCreateColliderRoot()
        {
            // Load preconfigured humanoid avatar prefab
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Add a single manual remap to a non-finger collider (Torso)
            var comp = root.AddComponent<ModularAvatarGlobalCollider>();
            comp.ManualRemap = true;
            comp.ColliderToHijack = GlobalCollider.Torso;
            comp.Radius = 0.5f;
            comp.Height = 0.6f;
            comp.Position = new Vector3(0.1f, 0.2f, 0.3f);
            comp.Rotation = Quaternion.Euler(5f, 6f, 7f);

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();
            Assert.NotNull(desc);

            // The torso collider should be set to custom
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_torso.state);
            Assert.AreEqual(0.5f, desc.collider_torso.radius, 1e-6f);
            Assert.AreEqual(0.6f, desc.collider_torso.height, 1e-6f);

            // For non-finger colliders, the pass should NOT create MA_ColliderRoot/Target objects
            var anyRoot = root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("MA_ColliderRoot_"));
            var anyTarget = root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("MA_ColliderTarget_"));
            Assert.IsFalse(anyRoot, "No MA_ColliderRoot_ objects should be created for non-finger colliders");
            Assert.IsFalse(anyTarget, "No MA_ColliderTarget_ objects should be created for non-finger colliders");

            // For non-finger colliders, the descriptor transform should be set to the component's RootTransform
            Assert.AreEqual(root.transform, desc.collider_torso.transform);

            // The component should be removed from the hierarchy
            Assert.AreEqual(0, root.GetComponentsInChildren<ModularAvatarGlobalCollider>(true).Length);
        }

        [Test]
        public void ManualOnlyMappingAppliesCorrectly()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            var manual = root.AddComponent<ModularAvatarGlobalCollider>();
            manual.ManualRemap = true;
            manual.ColliderToHijack = GlobalCollider.HandLeft; // non-finger
            manual.Radius = 0.11f;
            manual.Height = 0.22f;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_handL.state);
            Assert.AreEqual(0.11f, desc.collider_handL.radius, 1e-6f);
            Assert.AreEqual(0.22f, desc.collider_handL.height, 1e-6f);

            // All other colliders remain automatic
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_handR.state);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Automatic, desc.collider_head.state);
        }

        [Test]
        public void ManualPlusAutoNonInterfering()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Create separate GameObjects for each collider and attach the component to them
            var manualObj = CreateChild(root, "ManualColliderObj");
            var manual = manualObj.AddComponent<ModularAvatarGlobalCollider>();
            manual.ManualRemap = true;
            manual.ColliderToHijack = GlobalCollider.Torso; // non-finger
            manual.Radius = 0.2f;
            manual.Height = 0.3f;
            manual.RootTransform = manualObj.transform;

            var autoObj = CreateChild(root, "AutoColliderObj");
            var auto = autoObj.AddComponent<ModularAvatarGlobalCollider>();
            auto.ManualRemap = false;
            auto.Radius = 0.21f;
            auto.Height = 0.31f;
            auto.RootTransform = autoObj.transform;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();

            // Manual should have applied to torso
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_torso.state);
            Assert.AreEqual(0.2f, desc.collider_torso.radius, 1e-6f);

            // Auto should pick FingerRingLeft without interfering
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingL.state);
            Assert.AreEqual(0.21f, desc.collider_fingerRingL.radius, 1e-6f);
        }

        [Test]
        public void ManualPlusAutoInterfering()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Manual claims FingerRingLeft
            var manualObj = CreateChild(root, "ManualColliderObj2");
            var manual = manualObj.AddComponent<ModularAvatarGlobalCollider>();
            manual.ManualRemap = true;
            manual.ColliderToHijack = GlobalCollider.FingerRingLeft;
            manual.Radius = 0.3f;
            manual.Height = 0.4f;
            manual.RootTransform = manualObj.transform;

            // Auto also present on its own GameObject
            var autoObj = CreateChild(root, "AutoColliderObj2");
            var auto = autoObj.AddComponent<ModularAvatarGlobalCollider>();
            auto.ManualRemap = false;
            auto.Radius = 0.31f;
            auto.Height = 0.41f;
            auto.RootTransform = autoObj.transform;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();

            // Manual should have applied to FingerRingLeft
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingL.state);
            Assert.AreEqual(0.3f, desc.collider_fingerRingL.radius, 1e-6f);

            // Auto should have been pushed to next priority (FingerRingRight)
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingR.state);
            Assert.AreEqual(0.31f, desc.collider_fingerRingR.radius, 1e-6f);
        }

        [Test]
        public void MultipleManualAndAutoMappings()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Manual mappings
            var manualObj1 = CreateChild(root, "ManualColliderA");
            var manual1 = manualObj1.AddComponent<ModularAvatarGlobalCollider>();
            manual1.ManualRemap = true;
            manual1.ColliderToHijack = GlobalCollider.FingerRingLeft;
            manual1.Radius = 0.5f;
            manual1.Height = 0.6f;
            manual1.RootTransform = manualObj1.transform;

            var manualObj2 = CreateChild(root, "ManualColliderB");
            var manual2 = manualObj2.AddComponent<ModularAvatarGlobalCollider>();
            manual2.ManualRemap = true;
            manual2.ColliderToHijack = GlobalCollider.Torso;
            manual2.Radius = 0.55f;
            manual2.Height = 0.65f;
            manual2.RootTransform = manualObj2.transform;

            // Auto mappings
            var autoObj1 = CreateChild(root, "AutoColliderA");
            var auto1 = autoObj1.AddComponent<ModularAvatarGlobalCollider>();
            auto1.ManualRemap = false;
            auto1.Radius = 0.51f;
            auto1.Height = 0.61f;
            auto1.RootTransform = autoObj1.transform;

            var autoObj2 = CreateChild(root, "AutoColliderB");
            var auto2 = autoObj2.AddComponent<ModularAvatarGlobalCollider>();
            auto2.ManualRemap = false;
            auto2.Radius = 0.52f;
            auto2.Height = 0.62f;
            auto2.RootTransform = autoObj2.transform;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();

            // Manual should have applied
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingL.state);
            Assert.AreEqual(0.5f, desc.collider_fingerRingL.radius, 1e-6f);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_torso.state);
            Assert.AreEqual(0.55f, desc.collider_torso.radius, 1e-6f);

            // Autos should have taken next available finger colliders in priority order
            // First auto -> FingerRingRight, second auto -> FingerMiddleLeft
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingR.state);
            Assert.AreEqual(0.51f, desc.collider_fingerRingR.radius, 1e-6f);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerMiddleL.state);
            Assert.AreEqual(0.52f, desc.collider_fingerMiddleL.radius, 1e-6f);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Test]
        public void ManualOverwrittenByManual(bool lowPriority)
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Low-priority manual targeting FingerRingLeft
            var lowObj = CreateChild(root, "LowPriorityObj");
            var low = lowObj.AddComponent<ModularAvatarGlobalCollider>();
            low.ManualRemap = true;
            low.LowPriority = lowPriority;
            low.ColliderToHijack = GlobalCollider.FingerRingLeft;
            low.Radius = 0.11f;

            // Non-low manual targeting same collider should overwrite
            var highObj = CreateChild(root, "HighPriorityObj");
            var high = highObj.AddComponent<ModularAvatarGlobalCollider>();
            high.ManualRemap = true;
            high.LowPriority = false;
            high.ColliderToHijack = GlobalCollider.FingerRingLeft;
            high.Radius = 0.22f;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            var errors = nadena.dev.ndmf.ErrorReport.CaptureErrors(() =>
            {
                new VRChatGlobalColliderPass().TestExecute(buildContext);
            });

            var desc = root.GetComponent<VRCAvatarDescriptor>();
            // Final should be high's value
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingL.state);
            Assert.AreEqual(0.22f, desc.collider_fingerRingL.radius, 1e-6f);

            // Verify the manual-overwrite message key was not emitted
            Assert.AreEqual(!lowPriority, errors.Any(e => (e.TheError as nadena.dev.ndmf.SimpleError)?.TitleKey == "validation.global_collider.manual_collider_overwrite"));
        }

        [Test]
        public void LowPriorityManualDoesNotInterfereWithOtherManuals()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Low-priority manual on FingerRingLeft
            var lowObj = CreateChild(root, "LowPriorityObj2");
            var low = lowObj.AddComponent<ModularAvatarGlobalCollider>();
            low.ManualRemap = true;
            low.LowPriority = true;
            low.ColliderToHijack = GlobalCollider.FingerRingLeft;
            low.Radius = 0.13f;

            // Non-conflicting manual on FingerRingRight
            var otherObj = CreateChild(root, "OtherManualObj");
            var other = otherObj.AddComponent<ModularAvatarGlobalCollider>();
            other.ManualRemap = true;
            other.ColliderToHijack = GlobalCollider.FingerRingRight;
            other.Radius = 0.23f;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingL.state);
            Assert.AreEqual(0.13f, desc.collider_fingerRingL.radius, 1e-6f);
            Assert.AreEqual(VRCAvatarDescriptor.ColliderConfig.State.Custom, desc.collider_fingerRingR.state);
            Assert.AreEqual(0.23f, desc.collider_fingerRingR.radius, 1e-6f);
        }

        [Test]
        public void FillAllColliderSlotsWithMixOfAutoAndManual_ValidateExactAssignments()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Manual non-finger colliders
            var headObj = CreateChild(root, "HeadObj");
            var head = headObj.AddComponent<ModularAvatarGlobalCollider>(); head.ManualRemap = true; head.ColliderToHijack = GlobalCollider.Head; head.Radius = 0.01f;
            var torsoObj = CreateChild(root, "TorsoObj");
            var torso = torsoObj.AddComponent<ModularAvatarGlobalCollider>(); torso.ManualRemap = true; torso.ColliderToHijack = GlobalCollider.Torso; torso.Radius = 0.02f;
            var handLObj = CreateChild(root, "HandLObj");
            var handL = handLObj.AddComponent<ModularAvatarGlobalCollider>(); handL.ManualRemap = true; handL.ColliderToHijack = GlobalCollider.HandLeft; handL.Radius = 0.03f;
            var handRObj = CreateChild(root, "HandRObj");
            var handR = handRObj.AddComponent<ModularAvatarGlobalCollider>(); handR.ManualRemap = true; handR.ColliderToHijack = GlobalCollider.HandRight; handR.Radius = 0.04f;
            var footLObj = CreateChild(root, "FootLObj");
            var footL = footLObj.AddComponent<ModularAvatarGlobalCollider>(); footL.ManualRemap = true; footL.ColliderToHijack = GlobalCollider.FootLeft; footL.Radius = 0.05f;
            var footRObj = CreateChild(root, "FootRObj");
            var footR = footRObj.AddComponent<ModularAvatarGlobalCollider>(); footR.ManualRemap = true; footR.ColliderToHijack = GlobalCollider.FootRight; footR.Radius = 0.06f;

            // Manual finger collider for RingLeft
            var manualFingerObj = CreateChild(root, "ManualFingerObj");
            var manualFinger = manualFingerObj.AddComponent<ModularAvatarGlobalCollider>(); manualFinger.ManualRemap = true; manualFinger.ColliderToHijack = GlobalCollider.FingerRingLeft; manualFinger.Radius = 0.07f;

            // Add autos to fill remaining finger slots (7 remaining), with predictable radii
            for (int i = 0; i < 7; i++)
            {
                var obj = CreateChild(root, "AutoFill_" + i);
                var auto = obj.AddComponent<ModularAvatarGlobalCollider>();
                auto.ManualRemap = false;
                auto.Radius = 0.11f + i * 0.01f; // 0.11, 0.12, ...
            }

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var desc = root.GetComponent<VRCAvatarDescriptor>();

            // Check non-finger radii
            Assert.AreEqual(0.01f, desc.collider_head.radius, 1e-6f);
            Assert.AreEqual(0.02f, desc.collider_torso.radius, 1e-6f);
            Assert.AreEqual(0.03f, desc.collider_handL.radius, 1e-6f);
            Assert.AreEqual(0.04f, desc.collider_handR.radius, 1e-6f);
            Assert.AreEqual(0.05f, desc.collider_footL.radius, 1e-6f);
            Assert.AreEqual(0.06f, desc.collider_footR.radius, 1e-6f);

            // Check finger radii mapping explicitly
            Assert.AreEqual(0.07f, desc.collider_fingerRingL.radius, 1e-6f); // manual
            Assert.AreEqual(0.11f, desc.collider_fingerRingR.radius, 1e-6f); // auto 0
            Assert.AreEqual(0.12f, desc.collider_fingerMiddleL.radius, 1e-6f); // auto 1
            Assert.AreEqual(0.13f, desc.collider_fingerMiddleR.radius, 1e-6f); // auto 2
            Assert.AreEqual(0.14f, desc.collider_fingerLittleL.radius, 1e-6f); // auto 3
            Assert.AreEqual(0.15f, desc.collider_fingerLittleR.radius, 1e-6f); // auto 4
            Assert.AreEqual(0.16f, desc.collider_fingerIndexL.radius, 1e-6f); // auto 5
            Assert.AreEqual(0.17f, desc.collider_fingerIndexR.radius, 1e-6f); // auto 6
        }

        [Test]
        public void ErrorCase_ManualNone_EmitsInformation()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            var noneObj = CreateChild(root, "ManualNoneObjOnly");
            var none = noneObj.AddComponent<ModularAvatarGlobalCollider>();
            none.ManualRemap = true;
            none.ColliderToHijack = GlobalCollider.None;

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);

            var errors = nadena.dev.ndmf.ErrorReport.CaptureErrors(() =>
            {
                new VRChatGlobalColliderPass().TestExecute(buildContext);
            });

            // Validate exact build report key was emitted
            Assert.IsTrue(errors.Any(e => (e.TheError as nadena.dev.ndmf.SimpleError)?.TitleKey == "validation.global_collider.manual_collider_none"));
        }

        [Test]
        public void ErrorCase_AutoNoSlots_EmitsNonFatal()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            // Add many autos to exhaust finger slots (9 autos -> 1 will fail)
            for (int i = 0; i < 9; i++)
            {
                var obj = CreateChild(root, "AutoErrOnly_" + i);
                var auto = obj.AddComponent<ModularAvatarGlobalCollider>();
                auto.ManualRemap = false;
            }

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);

            var errors = nadena.dev.ndmf.ErrorReport.CaptureErrors(() =>
            {
                new VRChatGlobalColliderPass().TestExecute(buildContext);
            });

            // Validate the specific key for no slots was emitted
            Assert.IsTrue(errors.Any(e => (e.TheError as nadena.dev.ndmf.SimpleError)?.TitleKey == "validation.global_collider.no_global_colliders_available_vrc"));
        }

        [Test]
        public void CopyHijackedShape_CopiesDescriptorShapeIntoTarget()
        {
            var root = CreateCommonPrefab("ShapellAvatar.prefab");
            Assert.NotNull(root);

            var desc = root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            Assert.NotNull(desc);

            // Prepare a custom shape on the descriptor for Torso
            var src = desc.collider_torso;
            src.radius = 0.77f;
            src.height = 1.23f;
            src.position = new Vector3(0.1f, 0.2f, 0.3f);
            src.rotation = Quaternion.Euler(1f, 2f, 3f);
            desc.collider_torso = src;

            // Create a manual remap that would normally set different values, but with CopyHijackedShape = true we expect the descriptor values to be copied
            var obj = CreateChild(root, "CopySrcObj");
            var comp = obj.AddComponent<ModularAvatarGlobalCollider>();
            comp.ManualRemap = true;
            comp.ColliderToHijack = GlobalCollider.Torso;
            comp.CopyHijackedShape = true;
            // set different values on component to ensure copy happens
            comp.Radius = 0.11f;
            comp.Height = 0.22f;
            comp.Position = new Vector3(9f, 9f, 9f);
            comp.Rotation = Quaternion.Euler(9f, 9f, 9f);

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            new VRChatGlobalColliderPass().TestExecute(buildContext);

            var outDesc = root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            var outCfg = outDesc.collider_torso;

            Assert.AreEqual(0.77f, outCfg.radius, 1e-6f);
            Assert.AreEqual(1.23f, outCfg.height, 1e-6f);
            Assert.AreEqual(new Vector3(0.1f, 0.2f, 0.3f), outCfg.position);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(1f, 2f, 3f), outCfg.rotation), 1e-3f);

            // component should be removed
            Assert.AreEqual(0, root.GetComponentsInChildren<ModularAvatarGlobalCollider>(true).Length);
        }
    }
}
#endif
