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
        [Test]
        public void TestVRCFuryWarningWithMeshCutter()
        {
            var avatarRoot = CreateCommonAvatar("Test.prefab");
            var child = new GameObject("TestObject");
            child.transform.SetParent(avatarRoot.transform);
            
            // Add a MeshCutter component
            var meshCutter = child.AddComponent<ModularAvatarMeshCutter>();
            meshCutter.Object = new AvatarObjectReference();
            
            ErrorReport.Clear();
            ComponentValidation.ValidateAll(avatarRoot);
            
#if LEGACY_VRCFURY
            // Should have the VRCFury warning
            Assert.IsTrue(ErrorReport.HasErrorsOrWarnings());
            var errors = ErrorReport.GetErrors();
            Assert.IsTrue(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
#else
            // Should not have the VRCFury warning when LEGACY_VRCFURY is not defined
            var errors = ErrorReport.GetErrors();
            Assert.IsFalse(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
#endif
        }
        
        [Test]
        public void TestVRCFuryWarningWithShapeChangerDelete()
        {
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
            
#if LEGACY_VRCFURY
            // Should have the VRCFury warning
            Assert.IsTrue(ErrorReport.HasErrorsOrWarnings());
            var errors = ErrorReport.GetErrors();
            Assert.IsTrue(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
#else
            // Should not have the VRCFury warning when LEGACY_VRCFURY is not defined
            var errors = ErrorReport.GetErrors();
            Assert.IsFalse(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
#endif
        }
        
        [Test]
        public void TestNoVRCFuryWarningWithShapeChangerSet()
        {
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
            
            // Should not have the VRCFury warning even when LEGACY_VRCFURY is defined
            var errors = ErrorReport.GetErrors();
            Assert.IsFalse(errors.Any(e => e.Code == "validation.legacy_vrcfury_warning"));
        }
    }
}