#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using EditorCurveBinding = UnityEditor.EditorCurveBinding;

namespace ShapeChangerTests
{
    public class SCDefaultAnimation : TestBase
    {
        [Test]
        public void SetsCorrectInitialStatesAndAnimations()
        {
            SetsCorrectInitialStatesAndAnimations("SCDefaultAnimation.prefab");
        }

        [Test]
        public void SetsCorrectInitialStatesAndAnimationsForInactiveSC()
        {
            var root = CreatePrefab("SCDefaultAnimationInactive.prefab");
            AvatarProcessor.ProcessAvatar(root);

            var fx = (AnimatorController) FindFxController(root).animatorController;
            var baseLayer = fx.layers.FirstOrDefault(l => l.name == BakeContext.BASE_LAYER_NAME);
            var allClips = baseLayer?.stateMachine?.defaultState?.motion == null ? new List<AnimationClip>() : CollectClips(baseLayer.stateMachine.defaultState.motion).ToList();

            var smr = root.transform.Find("test mesh").GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = smr.sharedMesh;

            var curve = allClips.Select(c => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(
                "test mesh", typeof(SkinnedMeshRenderer), "blendShape.key1"
            ))).FirstOrDefault(c => c != null);
            Assert.IsNull(curve); // always off MenuItem (due to object disable), no curve should be generated

            curve = allClips.Select(c => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(
                "test mesh", typeof(SkinnedMeshRenderer), "blendShape.key2"
            ))).FirstOrDefault(c => c != null);
            // Always-on delete, no curve should be generated
            Assert.IsNull(curve);

            curve = allClips.Select(c => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(
                "test mesh", typeof(SkinnedMeshRenderer), "blendShape.key3"
            ))).FirstOrDefault(c => c != null);
            // Always-on set, no curve should be generated
            Assert.IsNull(curve);

            // Check actual blendshape states
            Assert.AreEqual(10.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key1")), 0.1f);
            Assert.AreEqual(100.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key3")), 0.1f);
        }

        [Test]
        public void SetsCorrectInitialStatesAndAnimationsForInvertedSC()
        {
            SetsCorrectInitialStatesAndAnimations("SCDefaultAnimationInverted.prefab");
        }

        private void SetsCorrectInitialStatesAndAnimations(string prefabPath)
        {
            var root = CreatePrefab(prefabPath);
            AvatarProcessor.ProcessAvatar(root);

            var fx = (AnimatorController) FindFxController(root).animatorController;
            var baseLayer = fx.layers.FirstOrDefault(l => l.name == BakeContext.BASE_LAYER_NAME);
            var allClips = baseLayer?.stateMachine?.defaultState?.motion == null ? new List<AnimationClip>() : CollectClips(baseLayer.stateMachine.defaultState.motion).ToList();

            var smr = root.transform.Find("test mesh").GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = smr.sharedMesh;

            var curve = allClips.Select(c => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(
                "test mesh", typeof(SkinnedMeshRenderer), "blendShape.key1"
            ))).FirstOrDefault(c => c != null);
            Assert.AreEqual(7.0f, curve.keys[0].value, 0.1f);
            Assert.AreEqual(7.0f, curve.keys[1].value, 0.1f);

            curve = allClips.Select(c => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(
                "test mesh", typeof(SkinnedMeshRenderer), "blendShape.key2"
            ))).FirstOrDefault(c => c != null);
            // Always-on delete, no curve should be generated
            Assert.IsNull(curve);

            curve = allClips.Select(c => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve(
                "test mesh", typeof(SkinnedMeshRenderer), "blendShape.key3"
            ))).FirstOrDefault(c => c != null);
            // Always-on set, no curve should be generated
            Assert.IsNull(curve);

            // Check actual blendshape states
            Assert.AreEqual(10.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key1")), 0.1f);
            Assert.AreEqual(100.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key3")), 0.1f);
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
}

#endif
