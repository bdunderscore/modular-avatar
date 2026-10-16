#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;

namespace nadena.dev.modular_avatar.test.MiscAnimationTests
{
    public class MiscAnimationTests : TestBase
    {
        [Test]
        public void HighQualityCurvesSettingPreserved()
        {
            var prefab = CreatePrefab("HighQualityCurvesSettingPreserved.prefab");
            AvatarProcessor.ProcessAvatar(prefab);
    
            var layer = findFxLayer(prefab, "Base Layer");
    
            var hq_on = FindStateInLayer(layer, "HQ_ON");
            var hq_off = FindStateInLayer(layer, "HQ_OFF");
    
            Assert.True(new SerializedObject(hq_on.motion).FindProperty("m_UseHighQualityCurve").boolValue);
            Assert.False(new SerializedObject(hq_off.motion).FindProperty("m_UseHighQualityCurve").boolValue);
        }
    }
}

#endif