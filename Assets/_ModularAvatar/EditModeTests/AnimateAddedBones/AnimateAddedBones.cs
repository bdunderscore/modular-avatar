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

            var layerName = "merged";

            var motion = findFxMotion(prefab, layerName);

            var cubeObject = prefab.transform.Find("Armature/Hips").GetChild(0).gameObject;
            Assert.True(cubeObject.name.StartsWith("Cube$"));

            var binding =
                EditorCurveBinding.FloatCurve("Armature/Hips/" + cubeObject.name, typeof(Transform),
                    "localEulerAnglesRaw.x");
            Assert.NotNull(AnimationUtility.GetEditorCurve(motion, binding));
        }
    }
}