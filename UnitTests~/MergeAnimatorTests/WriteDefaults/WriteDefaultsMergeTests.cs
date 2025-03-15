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
            [Values(true, false)] bool mergeSetMode
        )
        {
            bool? wdMode;
            switch (scenario)
            {
                case "WD_OFF": wdMode = false; break;
                case "WD_ON": wdMode = true; break;
                default: wdMode = null; break;
            }
            
            var root = CreateRoot(scenario);
            var m1 = CreateChild(root, "m1").AddComponent<ModularAvatarMergeAnimator>();
            var m2 = CreateChild(root, "m2").AddComponent<ModularAvatarMergeAnimator>();
            
            m1.animator = LoadAsset<AnimatorController>(scenario + ".controller");
            m1.mergeAnimatorMode = MergeAnimatorMode.Replace;
            m2.animator = LoadAsset<AnimatorController>("TestSet_" + mergeSetMode + ".controller");
            
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
                    case 'M': expectedState = wdMode ?? mergeSetMode; break;
                    case 'X': expectedState = mergeSetMode; break;
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