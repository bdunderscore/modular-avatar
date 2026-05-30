using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using BuildContext = nadena.dev.ndmf.BuildContext;

namespace modular_avatar_tests
{
    public class AOCRefreshTest : TestBase
    {
        [Test]
        public void Test()
        {
            var avatar = new GameObject("avatar");

            var ac = new AnimatorController();
            var originalMotion = new AnimationClip() { name = "c1" };
            var newMotion = new AnimationClip() { name = "c2" };
            var aoc = new AnimatorOverrideController(ac);
            
            ac.layers = new[]
            {
                new AnimatorControllerLayer()
                {
                    stateMachine = new AnimatorStateMachine()
                    {
                        states = new[]
                        {
                            new ChildAnimatorState()
                            {
                                state = new AnimatorState()
                                {
                                    motion = originalMotion
                                }
                            }
                        }
                    }
                }
            };
            ac.layers[0].stateMachine.defaultState = ac.layers[0].stateMachine.states[0].state;
            
            aoc.ApplyOverrides(new[] { new KeyValuePair<AnimationClip, AnimationClip>(originalMotion, newMotion) });

            var animator = avatar.AddComponent<Animator>();
            animator.runtimeAnimatorController = aoc;

            avatar.AddComponent<NDMFAvatarRoot>();
            
            var context = CreateContext(avatar, WellKnownPlatforms.Generic);

            var vcc = context.ActivateExtensionContextRecursive<VirtualControllerContext>();
            var virtualController = vcc.Controllers[animator];
            Assert.AreEqual("c2", virtualController.Layers.First().StateMachine.DefaultState.Motion.Name);
            
            context.DeactivateAllExtensionContexts();
            
            // VCC should have turned this into a normal animator controller. modify it (in any way)
            ac = (AnimatorController)animator.runtimeAnimatorController;
            
            Assert.AreEqual("c2", ac.layers[0].stateMachine.defaultState.motion.name);
            
            ac.layers = new AnimatorControllerLayer[]
            {
                ac.layers[0],
                new AnimatorControllerLayer()
                {
                    stateMachine = new AnimatorStateMachine()
                }
            };
            
            vcc = context.ActivateExtensionContextRecursive<VirtualControllerContext>();
            virtualController = vcc.Controllers[animator];
            Assert.AreEqual("c2", virtualController.Layers.First().StateMachine.DefaultState.Motion.Name);

            context.DeactivateAllExtensionContexts();
            
            // The animator should still use the replaced motion
            Assert.AreEqual(newMotion.name, ac.layers[0].stateMachine.defaultState.motion.name);
        }
    }
}