using System.Collections;
using System.Collections.Generic;
using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;


namespace _ModularAvatar.EditModeTests
{
    public class AnimationDatabaseCloningTest : TestBase
    {
        [Test]
        public void TestAnimationDatabaseCloningLogic()
        {
            var root = CreateRoot("root");
            var context = CreateContext(root);
            
            var origController = LoadAsset<AnimatorController>("ac.controller");
            var state = origController.layers[0].stateMachine.defaultState;
            var clonedState = Object.Instantiate(state);

            var origAnimation = LoadAsset<AnimationClip>("anim.anim");

            using (new ObjectRegistryScope(new ObjectRegistry(root.transform)))
            {
                var db = new AnimationDatabase();
                db.OnActivate(context);
                db.RegisterState(clonedState);

                var newBlendTree = clonedState.motion as BlendTree;
                var origBlendTree = state.motion as BlendTree;

                Assert.NotNull(newBlendTree);
                Assert.NotNull(origBlendTree);

                Assert.AreNotSame(newBlendTree, origBlendTree);
                Assert.AreNotSame(newBlendTree.children[1].motion, origBlendTree.children[1].motion);

                // Before commit, proxy animations are replaced.
                Assert.AreNotSame(newBlendTree.children[0].motion, origBlendTree.children[0].motion);

                Assert.AreSame(ObjectRegistry.GetReference(origAnimation),
                    ObjectRegistry.GetReference(newBlendTree.children[1].motion));

                db.Commit();

                Assert.AreNotSame(newBlendTree, origBlendTree);
                Assert.AreNotSame(newBlendTree.children[1].motion, origBlendTree.children[1].motion);

                // After commit, proxy animations are restored to the original assets.
                Assert.AreSame(newBlendTree.children[0].motion, origBlendTree.children[0].motion);
            }
        }
    }
}