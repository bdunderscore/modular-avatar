using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace modular_avatar_tests.TransformMappingThroughSwitchedObject
{
    /// <summary>
    /// This test verifies that transform mappings are properly handled, even if the bone they target is on an armature
    /// underneath multiple switched objects (which therefore would generate multiple levels of proxy switch objects).
    /// </summary>
    public class TransformMappingThroughSwitchedObject : TestBase
    {
        [Test]
        public void TransformMappingHandledCorrectly()
        {
            var prefab = CreatePrefab("TransformMappingThroughSwitchedObject.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var motion = findFxMotion(prefab, "child_controller");

            var binding = EditorCurveBinding.FloatCurve("Armature/Hips", typeof(Transform), "localEulerAnglesRaw.x");
            var curve = AnimationUtility.GetEditorCurve(motion, binding);
            Assert.IsNotNull(curve);
        }
    }
}