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

using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    /**
     * This abstract base class is injected into the VRCSDK avatar component allowlist to avoid
     */
    [DefaultExecutionOrder(-9999)] // run before av3emu
    public abstract class AvatarTagComponent : MonoBehaviour
    {
        private void Awake()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;
            RuntimeUtil.OnDemandProcessAvatar(RuntimeUtil.OnDemandSource.Awake, this);
        }

        private void Start()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;
            RuntimeUtil.OnDemandProcessAvatar(RuntimeUtil.OnDemandSource.Start, this);
        }

        private void OnValidate()
        {
            if (RuntimeUtil.isPlaying) return;

            RuntimeUtil.delayCall(() =>
            {
                if (this == null) return;
#if UNITY_EDITOR
                Activator.CreateIfNotPresent(gameObject.scene);
#endif
            });
        }
    }
}