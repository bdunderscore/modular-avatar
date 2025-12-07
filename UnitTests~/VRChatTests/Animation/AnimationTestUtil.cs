using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace modular_avatar_tests
{
    public static class AnimationTestUtil
    {
        public static AnimatorController TestController(string name, Motion motion = null)
        {
            var controller = new AnimatorController();
            var stateMachine = new AnimatorStateMachine();
            var state = new AnimatorState();
            state.name = name;
            
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    defaultWeight = 1,
                    name = name,
                    stateMachine = stateMachine
                }
            };
            
            stateMachine.name = name;
            stateMachine.states = new[]
            {
                new ChildAnimatorState()
                {
                    state = state
                }
            };
            stateMachine.defaultState = state;
            state.motion = motion;

            return controller;
        }

        public static AnimationClip AnimationWithPath(string path)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, 1, 0));
            return clip;
        }

        public static void AssertAnimationHasPath(AnimationClip clip, string path)
        {
            Assert.IsTrue(AnimationUtility.GetCurveBindings(clip).Any(b => b.path == path));
        }
    }
}