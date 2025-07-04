#if MA_VRCSDK3_AVATARS

using System;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
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
            var baseBlend = (BlendTree) fxc.layers[0].stateMachine.defaultState.motion;
            var subBlend = (BlendTree) baseBlend.children[0].motion;
            var animStateMotion = (AnimationClip) subBlend.children[0].motion;

            foreach (var key in AnimationUtility.GetCurveBindings(animStateMotion))
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
            
            Assert.AreEqual(staticState, component.enabled);

            var fx = FindFxController(prefab);
            var fxc = (AnimatorController)fx.animatorController;
            var baseBlend = (BlendTree) fxc.layers[0].stateMachine.defaultState.motion;
            var subBlend = (BlendTree) baseBlend.children[0].motion;
            var animStateMotion = (AnimationClip) subBlend.children[0].motion;

            var binding = EditorCurveBinding.FloatCurve(name, componentType, "m_Enabled");
            var curve = AnimationUtility.GetEditorCurve(animStateMotion, binding);
            
            Assert.AreEqual(animState == null, curve == null);
            if (animState == null) return;
            
            var value = curve.keys[0].value;
            
            Assert.AreEqual(animState, value > 0.5f);
        }
    }
}

#endif