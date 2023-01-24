using System.Collections;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace modular_avatar_tests.SimpleArmatureToggle
{
    public class SimpleArmatureToggle : TestBase
    {
        [Test]
        public void ToplevelAssetTogglingWorks()
        {
            var prefab = CreatePrefab("SimpleArmatureToggle.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var layerName = "merged";
            var motion = findFxMotion(prefab, layerName);

            var obj1 = prefab.transform.Find("Armature/Hips").GetChild(1);
            var obj2 = prefab.transform.Find("Armature/Hips/Chest").GetChild(0);

            var binding1 = EditorCurveBinding.FloatCurve(RuntimeUtil.AvatarRootPath(obj1.gameObject),
                typeof(GameObject), "m_IsActive");
            var binding2 = EditorCurveBinding.FloatCurve(RuntimeUtil.AvatarRootPath(obj2.gameObject),
                typeof(GameObject), "m_IsActive");
            
            Assert.NotNull(AnimationUtility.GetEditorCurve(motion, binding1));
            Assert.NotNull(AnimationUtility.GetEditorCurve(motion, binding2));
        }
    }
}