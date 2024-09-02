using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class BaseLayerReferenceCorrectionTest : TestBase
    {
        [Test]
        public void whenBaseAnimatorLayersAreDeleted_layerCrossRefsAreCorrected()
        {
            var avatar = CreatePrefab("BaseLayerReferenceCorrection.prefab");
            
            AvatarProcessor.ProcessAvatar(avatar);

            var fx = findFxLayer(avatar, "test");
            var state = fx.stateMachine.defaultState;
            var alc = state.behaviours[0] as VRCAnimatorLayerControl;
            Assert.NotNull(alc);

            var desiredIndex = ((AnimatorController)FindFxController(avatar).animatorController)
                .layers.TakeWhile(l => l.name != "l1").Count();
            
            Assert.AreEqual(desiredIndex, alc.layer);
        }
    }
}