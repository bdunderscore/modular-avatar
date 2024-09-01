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
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDKBase;
#endif

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// This class is used internally by Modular Avatar for common operations between MA components. It should not be
    /// inherited by user classes, and will be removed in Modular Avatar 2.0.
    /// </summary>
    [DefaultExecutionOrder(-9999)] // run before av3emu
    public abstract class AvatarTagComponent : MonoBehaviour, IEditorOnly
    {
        internal static event Action OnChangeAction;

        protected virtual void OnValidate()
        {
            if (RuntimeUtil.isPlaying) return;

            OnChangeAction?.Invoke();
        }

        protected virtual void OnDestroy()
        {
            OnChangeAction?.Invoke();
        }

        /// <summary>
        /// Eagerly resolve all AvatarTagReferences to their destinations.
        /// </summary>
        public virtual void ResolveReferences()
        {
        }
    }
    
#if !MA_VRCSDK3_AVATARS

    /**
     * Placeholder of VRC.SDKBase.IEditorOnly for environments without VRCSDK
     */
    interface IEditorOnly
    {
    }

#endif
}