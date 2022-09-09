/*
 * MIT License
 * 
 * Copyright (c) 2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

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