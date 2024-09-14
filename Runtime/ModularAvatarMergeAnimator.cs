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

#if MA_VRCSDK3_AVATARS

using System;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core
{
    public enum MergeAnimatorPathMode
    {
        Relative,
        Absolute
    }

    [AddComponentMenu("Modular Avatar/MA Merge Animator")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/merge-animator?lang=auto")]
    public class ModularAvatarMergeAnimator : AvatarTagComponent
    {
        public RuntimeAnimatorController animator;
        public VRCAvatarDescriptor.AnimLayerType layerType = VRCAvatarDescriptor.AnimLayerType.FX;
        public bool deleteAttachedAnimator;
        public MergeAnimatorPathMode pathMode = MergeAnimatorPathMode.Relative;
        public bool matchAvatarWriteDefaults;
        public AvatarObjectReference relativePathRoot = new AvatarObjectReference();
        public int layerPriority = 0;

        public override void ResolveReferences()
        {
            // no-op
        }

        private void Reset()
        {
            // Init settings only when added or reset manually from the Inspector.
            // Otherwise, some plugins that add this component may break in non-playmode builds.
            if (RuntimeUtil.IsResetFromInspector())
            {
                InitSettings();
            }
        }

        internal void InitSettings()
        {
            deleteAttachedAnimator = true;
        }
    }
}

#endif