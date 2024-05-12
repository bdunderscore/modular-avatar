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

#region

using System;
using System.Runtime.CompilerServices;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;

#endregion

[assembly: InternalsVisibleTo("Tests")]

namespace nadena.dev.modular_avatar.core.editor
{
    public class AvatarProcessor
    {
        [MenuItem(UnityMenuItems.GameObject_ManualBake, true, UnityMenuItems.GameObject_ManualBakeOrder)]
        static bool ValidateApplyToCurrentAvatarGameobject()
        {
            return ValidateApplyToCurrentAvatar();
        }

        [MenuItem(UnityMenuItems.GameObject_ManualBake, false, UnityMenuItems.GameObject_ManualBakeOrder)]
        static void ApplyToCurrentAvatarGameobject()
        {
            ApplyToCurrentAvatar();
        }

        [MenuItem(UnityMenuItems.TopMenu_ManualBakeAvatar, true, UnityMenuItems.TopMenu_ManualBakeAvatarOrder)]
        private static bool ValidateApplyToCurrentAvatar()
        {
            return ndmf.AvatarProcessor.CanProcessObject(Selection.activeGameObject);
        }

        [MenuItem("Tools/Modular Avatar/Manual bake avatar", false)]
        private static void ApplyToCurrentAvatar()
        {
            ndmf.AvatarProcessor.ProcessAvatarUI(Selection.activeGameObject);
        }

        public static void ProcessAvatar(GameObject avatarGameObject)
        {
            ndmf.AvatarProcessor.ProcessAvatar(avatarGameObject);
        }

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once MemberCanBeMadeStatic.Global
        [Obsolete("This is only for compile time compatibility with legacy AAO")]
        public int callbackOrder => throw new NotImplementedException();

        [Obsolete("This is only for compile time compatibility with legacy AAO")]
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public bool OnPreprocessAvatar(GameObject avatarGameObject) => throw new NotImplementedException();
    }
}