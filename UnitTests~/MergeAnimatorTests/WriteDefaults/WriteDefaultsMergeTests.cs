using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;

namespace UnitTests.MergeAnimatorTests.WriteDefaults
{
    public class WriteDefaultsMergeTests : TestBase
    {
        [Test]
        public void TestWriteDefaultsMerge(
            [Values("WD_OFF", "WD_ON", "Ambiguous")] string scenario,
            [Values(true, false)] bool mergeAnimatorInitialState,
            [Values(true, false)] bool matchWD
        )
        {
            bool? baseFxWdState;
            switch (scenario)
            {
                case "WD_OFF": baseFxWdState = false; break;
                case "WD_ON": baseFxWdState = true; break;
                default: baseFxWdState = null; break;
            }

            // If the base layer is ambiguous and WD is disabled, the only thing we can assert is that nothing changed.
            // This is kind of a pain, so... TODO
            if (baseFxWdState == null && !matchWD) return;
            
            var root = CreateRoot(scenario);
            var m1 = CreateChild(root, "m1").AddComponent<ModularAvatarMergeAnimator>();
            var m2 = CreateChild(root, "m2").AddComponent<ModularAvatarMergeAnimator>();
            
            // m1 provides the base FX layer for the avatar
            m1.animator = LoadAsset<AnimatorController>(scenario + ".controller");
            m1.mergeAnimatorMode = MergeAnimatorMode.Replace;
            
            m2.animator = LoadAsset<AnimatorController>("TestSet_" + mergeAnimatorInitialState + ".controller");
            m2.mergeAnimatorMode = MergeAnimatorMode.Append;
            m2.matchAvatarWriteDefaults = matchWD;
            
            AvatarProcessor.ProcessAvatar(root);
            
            var fx = FindFxController(root);
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var vfx = cloneContext.Clone(fx.animatorController);

            foreach (var layer in vfx.Layers)
            {
                bool expectedState;
                
                if (MMDRelayPass.IsRelayLayer(layer.Name)) continue;
                
                switch (layer.Name[0])
                {
                    // M layers: We expect to "M"atch WD state if the merge component is set to match WD (and a WD mode
                    // was determined); otherwise, it should keep its original state.
                    case 'M':
                    {
                        expectedState = (matchWD ? baseFxWdState : null) ?? mergeAnimatorInitialState;
                        break;
                    }
                    case 'X': expectedState = mergeAnimatorInitialState; break;
                    case '1': expectedState = true; break;
                    default: continue;
                }

                foreach (var node in layer.AllReachableNodes())
                {
                    if (node is VirtualState vs)
                    {
                        Assert.AreEqual(expectedState, vs.WriteDefaultValues, "Layer " + layer.Name + " state " + vs.Name);
                    }
                }
            }
        }
    }
}