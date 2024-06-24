using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

#if MA_VRCSDK3_AVATARS_3_5_2_OR_NEWER
namespace modular_avatar_tests
{
    public class PlayAudioRemapping : TestBase
    {
        [Test]
        public void PlayAudioBehaviorsAreRemappedToCorrectPaths()
        {
            var prefab = CreatePrefab("PlayAudioRemapping.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = FindFxController(prefab);
            var ac = (AnimatorController)fx.animatorController;
            
            var layer = ac.layers[0];
            var state = layer.stateMachine.states[0].state;
            var playAudio = (VRCAnimatorPlayAudio) state.behaviours[0];
            Assert.AreEqual("New Parent/Bone Proxy/Audio Source", playAudio.SourcePath);
            
            var subState = layer.stateMachine.stateMachines[0].stateMachine.states[0].state;
            var playAudio2 = (VRCAnimatorPlayAudio) subState.behaviours[0];
            Assert.AreEqual("New Parent/Bone Proxy/Audio Source", playAudio2.SourcePath);
        }
    }
}
#endif