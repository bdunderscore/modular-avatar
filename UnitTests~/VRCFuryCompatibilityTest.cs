using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    public class VRCFuryCompatibilityTest : TestBase
    {
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            // Reset the test flag
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = false;
        }
        
        [TearDown]
        public override void Teardown()
        {
            // Reset the test flag to avoid breaking subsequent tests
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = false;
            base.Teardown();
        }
        
        [Test]
        public void TestVRCFuryWarningWithMeshCutter()
        {
            // Simulate legacy VRCFury presence
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = true;
            
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child = new GameObject("TestObject");
            child.transform.SetParent(avatarRoot.transform);
            
            // Add a MeshCutter component
            var meshCutter = child.AddComponent<ModularAvatarMeshCutter>();
            meshCutter.Object = new AvatarObjectReference();
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
            // Should have the VRCFury warning
            Assert.IsTrue(ErrorReport.HasErrorsOrWarnings());
            var errors = ErrorReport.GetErrors();
            Assert.IsTrue(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
        
        [Test]
        public void TestVRCFuryWarningWithShapeChangerDelete()
        {
            // Simulate legacy VRCFury presence
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = true;
            
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child = new GameObject("TestObject");
            child.transform.SetParent(avatarRoot.transform);
            
            // Add a ShapeChanger component with Delete mode
            var shapeChanger = child.AddComponent<ModularAvatarShapeChanger>();
            shapeChanger.Shapes = new System.Collections.Generic.List<ChangedShape>
            {
                new ChangedShape
                {
                    Object = new AvatarObjectReference(),
                    ShapeName = "TestShape",
                    ChangeType = ShapeChangeType.Delete,
                    Value = 1.0f
                }
            };
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
            // Should have the VRCFury warning
            Assert.IsTrue(ErrorReport.HasErrorsOrWarnings());
            var errors = ErrorReport.GetErrors();
            Assert.IsTrue(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
        
        [Test]
        public void TestNoVRCFuryWarningWithShapeChangerSet()
        {
            // Simulate legacy VRCFury presence
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = true;
            
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child = new GameObject("TestObject");
            child.transform.SetParent(avatarRoot.transform);
            
            // Add a ShapeChanger component with Set mode (should not trigger warning)
            var shapeChanger = child.AddComponent<ModularAvatarShapeChanger>();
            shapeChanger.Shapes = new System.Collections.Generic.List<ChangedShape>
            {
                new ChangedShape
                {
                    Object = new AvatarObjectReference(),
                    ShapeName = "TestShape",
                    ChangeType = ShapeChangeType.Set,
                    Value = 1.0f
                }
            };
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
            // Should not have the VRCFury warning even when legacy VRCFury is simulated
            var errors = ErrorReport.GetErrors();
            Assert.IsFalse(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
        
        [Test]
        public void TestNoVRCFuryWarningWithEmptyShapeChanger()
        {
            // Simulate legacy VRCFury presence
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = true;
            
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child = new GameObject("TestObject");
            child.transform.SetParent(avatarRoot.transform);
            
            // Add a ShapeChanger component with null shapes (should not trigger warning)
            var shapeChanger = child.AddComponent<ModularAvatarShapeChanger>();
            shapeChanger.Shapes = null;
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
            // Should not have the VRCFury warning
            var errors = ErrorReport.GetErrors();
            Assert.IsFalse(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
        
        [Test]
        public void TestNoVRCFuryWarningWhenNotLegacy()
        {
            // Don't simulate legacy VRCFury (modern VRCFury or no VRCFury)
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = false;
            
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child = new GameObject("TestObject");
            child.transform.SetParent(avatarRoot.transform);
            
            // Add a MeshCutter component that would normally trigger the warning
            var meshCutter = child.AddComponent<ModularAvatarMeshCutter>();
            meshCutter.Object = new AvatarObjectReference();
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
            // Should not have the VRCFury warning when legacy VRCFury is not present
            var errors = ErrorReport.GetErrors();
            Assert.IsFalse(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
        
        [Test]
        public void TestVRCFuryWarningOnlyWhenBothConditionsMet()
        {
            // Simulate legacy VRCFury presence
            ComponentValidation.ForceEnableLegacyVRCFuryWarning = true;
            
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child1 = new GameObject("TestObject1");
            child1.transform.SetParent(avatarRoot.transform);
            
            // Test case: only MeshCutter, should trigger warning
            var meshCutter = child1.AddComponent<ModularAvatarMeshCutter>();
            meshCutter.Object = new AvatarObjectReference();
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
            // Should trigger warning when legacy VRCFury is simulated and MeshCutter is present
            var errors = ErrorReport.GetErrors();
            Assert.IsTrue(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
    }
}