using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.AnimateAddedBones
{
    /// <summary>
    /// This test verifies that merged animations which affect a bone newly added to an armature by Merge Armature
    /// are properly adjusted for the new bone path.
    /// </summary>
    public class AnimateAddedBones : TestBase
    {
        [Test]
        public void AnimatesAddedBones()
        {
            var prefab = CreatePrefab("AnimateAddedBones.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);

            Assert.NotNull(fx);
            var ac = fx.animatorController as AnimatorController;
            Assert.NotNull(ac);
            Assert.False(fx.isDefault);

            var layer = ac.layers.FirstOrDefault(l => l.name == "merged");
            Assert.NotNull(layer);
            var state = layer.stateMachine.states[0].state;
            Assert.NotNull(state);

            var motion = state.motion as AnimationClip;
            Assert.NotNull(motion);

            var cubeObject = prefab.transform.Find("Armature/Hips").GetChild(0).gameObject;
            Assert.True(cubeObject.name.StartsWith("Cube$"));

            var binding =
                EditorCurveBinding.FloatCurve("Armature/Hips/" + cubeObject.name, typeof(Transform),
                    "localEulerAnglesRaw.x");
            Assert.NotNull(AnimationUtility.GetEditorCurve(motion, binding));
        }
    }
}