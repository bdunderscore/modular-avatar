using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class StaticOverrideTest : TestBase
{
    [Test]
    public void WhenFXWritesBlendshape_CannotElideResponsiveAnimation()
    {
        var prefab = CreatePrefab("StaticOverrideTest.prefab");

        AvatarProcessor.ProcessAvatar(prefab);
        
        var avDesc = prefab.GetComponent<VRCAvatarDescriptor>();
        var fx = (AnimatorController) FindFxController(prefab).animatorController;

        float? foundValue = null;
        foreach (var clip in fx.animationClips)
        {
            if (clip == null) continue;
            var curve = AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("mesh", typeof(SkinnedMeshRenderer), "blendShape.bottom")
            );
            if (curve != null && curve.keys.Length > 0)
            {
                foundValue = curve.keys[0].value;
            }
        }

        Assert.AreEqual(50.0f, foundValue);
    }

    [Test] 
    public void WhenFXDoesNotWriteBlendshape_CanElideResponsiveAnimation()
    {
        var prefab = CreatePrefab("StaticOverrideTest.prefab");
        var avDesc = prefab.GetComponent<VRCAvatarDescriptor>();
        for (int i = 0; i < avDesc.baseAnimationLayers.Length; i++)
        {
            avDesc.baseAnimationLayers[i].isDefault = true;
            avDesc.baseAnimationLayers[i].animatorController = null;
        }
        
        AvatarProcessor.ProcessAvatar(prefab);
        
        var fx = (AnimatorController) FindFxController(prefab).animatorController;

        float? foundValue = null;
        foreach (var layer in fx.layers)
        {
            var defaultState = layer.stateMachine.defaultState;
            if (defaultState.motion is not AnimationClip clip) continue;
            
            var curve = AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("mesh", typeof(SkinnedMeshRenderer), "blendShape.bottom")
            );
            if (curve != null)
            {
                foundValue = curve.keys[0].value;
            }
        }
        
        Assert.IsNull(foundValue);
    }
    
}
