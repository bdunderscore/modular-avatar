using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
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
            var baseLayer = fx.layers[0];
            
            var bt = baseLayer.stateMachine.states[0].state.motion as BlendTree;
            Assert.NotNull(bt);
            var subBt = bt.children[0].motion as BlendTree;
            Assert.NotNull(subBt);
            var clip = subBt.children[0].motion as AnimationClip;
            Assert.NotNull(clip);
            
            var smr = root.transform.Find("test mesh").GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = smr.sharedMesh;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                "test mesh",
                typeof(SkinnedMeshRenderer),
                "blendShape.key1"
            ));
            Assert.IsNull(curve); // always off MenuItem (due to object disable), no curve should be generated
            
            curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                "test mesh",
                typeof(SkinnedMeshRenderer),
                "blendShape.key2"
            ));
            // Always-on delete, no curve should be generated
            Assert.IsNull(curve);
            
            curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                "test mesh",
                typeof(SkinnedMeshRenderer),
                "blendShape.key3"
            ));
            // Always-on set, no curve should be generated
            Assert.IsNull(curve);
            
            // Check actual blendshape states
            Assert.AreEqual(10.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key1")), 0.1f);
            Assert.AreEqual(5.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key2")), 0.1f);
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
            var baseLayer = fx.layers[0];
            
            var bt = baseLayer.stateMachine.states[0].state.motion as BlendTree;
            Assert.NotNull(bt);
            var subBt = bt.children[0].motion as BlendTree;
            Assert.NotNull(subBt);
            var clip = subBt.children[0].motion as AnimationClip;
            Assert.NotNull(clip);
            
            var smr = root.transform.Find("test mesh").GetComponent<SkinnedMeshRenderer>();
            var sharedMesh = smr.sharedMesh;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                "test mesh",
                typeof(SkinnedMeshRenderer),
                "blendShape.key1"
            ));
            Assert.AreEqual(7.0f, curve.keys[0].value, 0.1f);
            Assert.AreEqual(7.0f, curve.keys[1].value, 0.1f);
            
            curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                "test mesh",
                typeof(SkinnedMeshRenderer),
                "blendShape.key2"
            ));
            // Always-on delete, no curve should be generated
            Assert.IsNull(curve);
            
            curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                "test mesh",
                typeof(SkinnedMeshRenderer),
                "blendShape.key3"
            ));
            // Always-on set, no curve should be generated
            Assert.IsNull(curve);
            
            // Check actual blendshape states
            Assert.AreEqual(10.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key1")), 0.1f);
            Assert.AreEqual(5.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key2")), 0.1f);
            Assert.AreEqual(100.0f, smr.GetBlendShapeWeight(sharedMesh.GetBlendShapeIndex("key3")), 0.1f);
        }
    }
}