using System.Collections;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.rc;
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

        var binding = EditorCurveBinding.FloatCurve("mesh", typeof(SkinnedMeshRenderer), "blendShape.bottom");
        float? foundValue = null;
        foreach (var layer in fx.layers.Where(l => l.name == BakeContext.APPLY_LAYER_NAME))
        foreach (var clip in CollectClips(layer.stateMachine.defaultState.motion))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null)
            {
                Debug.Log($"Found curve in layer {layer.name} with value {curve.keys[0].value}");
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

        var binding = EditorCurveBinding.FloatCurve("mesh", typeof(SkinnedMeshRenderer), "blendShape.bottom");
        float? foundValue = null;
        foreach (var layer in fx.layers.Where(l => l.name == BakeContext.APPLY_LAYER_NAME))
        foreach (var clip in CollectClips(layer.stateMachine.defaultState.motion))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve != null) foundValue = curve.keys[0].value;
        }

        Assert.IsNull(foundValue);
    }

    private static IEnumerable<AnimationClip> CollectClips(Motion motion)
    {
        if (motion is AnimationClip clip)
        {
            yield return clip;
        }
        else if (motion is BlendTree bt)
        {
            foreach (var child in bt.children)
            foreach (var c in CollectClips(child.motion))
                yield return c;
        }
    }
}
