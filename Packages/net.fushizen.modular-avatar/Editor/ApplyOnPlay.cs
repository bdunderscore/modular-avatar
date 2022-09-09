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

using UnityEditor;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    public static class ApplyOnPlay
    {
        private const string MENU_NAME = "Tools/Modular Avatar/Apply on Play";

        static ApplyOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Menu.SetChecked(MENU_NAME, ModularAvatarSettings.applyOnPlay);
        }

        [MenuItem(MENU_NAME)]
        private static void ToggleApplyOnPlay()
        {
            ModularAvatarSettings.applyOnPlay = !ModularAvatarSettings.applyOnPlay;
            Menu.SetChecked(MENU_NAME, ModularAvatarSettings.applyOnPlay);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredPlayMode && ModularAvatarSettings.applyOnPlay)
            {
                // TODO - only apply modular avatar changes?
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>(true))
                    {
                        if (avatar.GetComponentsInChildren<AvatarTagComponent>(true).Length > 0)
                        {
                            VRCBuildPipelineCallbacks.OnPreprocessAvatar(avatar.gameObject);
                        }
                    }
                }
            }
        }
    }
}