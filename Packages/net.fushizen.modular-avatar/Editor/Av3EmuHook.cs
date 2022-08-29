using System;
using System.Reflection;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    internal class Av3EmuHook
    {
        static Av3EmuHook()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var runtime = assembly.GetType("LyumaAv3Runtime");
                    if (runtime == null) continue;

                    var addHook = runtime.GetMethod("AddInitAvatarHook", BindingFlags.Static | BindingFlags.Public);
                    if (addHook == null) continue;

                    addHook.Invoke(null, new object[]
                    {
                        -999999,
                        (Action<VRCAvatarDescriptor>)(av => VRCBuildPipelineCallbacks.OnPreprocessAvatar(av.gameObject))
                    });
                    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

                    break;
                }
            }
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.ExitingPlayMode)
            {
                Util.DeleteTemporaryAssets();
            }
        }
    }
}