#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;

namespace UnitTests.ReactiveComponent
{
    public class StaticStateTests : TestBase
    {
        [Test]
        public void DisablesStaticStateForActiveComponents()
        {
            var prefab = CreatePrefab("RCDisablesActiveComponentStaticStates.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            AssertStaticState(prefab, "AudioSource", typeof(AudioSource), false, true);
            AssertStaticState(prefab, "ParentConstraint", typeof(ParentConstraint), true, null);
            AssertStaticState(prefab, "InitiallyDisabled", typeof(AudioSource), false, false);
        }

        [Test]
        public void DoesntTouchNonToggled()
        {
            var prefab = CreatePrefab("RCDisablesActiveComponentStaticStates.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            var rootMotion = BaseLayer(fxc).stateMachine.defaultState.motion;

            foreach (var clip in CollectClips(rootMotion))
            foreach (var key in AnimationUtility.GetCurveBindings(clip))
            {
                Assert.IsFalse(key.path.StartsWith("Uncontrolled"));
            }
            
            foreach (var b in prefab.transform.Find("Uncontrolled").GetComponentsInChildren<Behaviour>())
            {
                Assert.IsTrue(b.enabled);
            }
        }

        [Test]
        public void TracksChildComponents()
        {
            var prefab = CreatePrefab("RCDisablesActiveComponentStaticStates.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            AssertStaticState(prefab, "Parent/Child", typeof(AudioSource), false, true);
        }

        private void AssertStaticState(GameObject prefab, string name, Type componentType, bool staticState, bool? animState)
        {
            var child = prefab.transform.Find(name);
            
            var component = (Behaviour) child.GetComponent(componentType);
            
            Assert.AreEqual(staticState, component.enabled, "static state for " + name + " should be " + staticState + " but is " + component.enabled);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            var rootMotion = BaseLayer(fxc).stateMachine.defaultState.motion;

            var binding = EditorCurveBinding.FloatCurve(name, componentType, "m_Enabled");
            var curve = CollectClips(rootMotion)
                .Select(c => AnimationUtility.GetEditorCurve(c, binding))
                .FirstOrDefault(c => c != null);

            Assert.AreEqual(animState == null, curve == null);
            if (animState == null) return;

            var value = curve.keys[0].value;

            Assert.AreEqual(animState, value > 0.5f);
        }

        private AnimatorControllerLayer BaseLayer(AnimatorController ac)
        {
            return ac.layers.First(l => l.name == BakeContext.BASE_LAYER_NAME);
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