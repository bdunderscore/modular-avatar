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

namespace net.fushizen.modular_avatar.core
{
    [ExecuteInEditMode]
    public class MAInternalOffsetMarker : AvatarTagComponent
    {
        void OnValidate()
        {
#if MODULAR_AVATAR_DEBUG
            hideFlags = HideFlags.None;
#else
            hideFlags = HideFlags.HideInInspector;
#endif
        }

        private const float POS_EPSILON = 0.01f;
        private const float ROT_EPSILON = 0.01f;
        
        private Vector3 lastLocalPos;
        private Vector3 lastLocalScale;
        private Quaternion lastLocalRot;
        
        public Transform correspondingObject;

        public bool lockBasePosition;
        
        public void Update()
        {
            if (correspondingObject == null) return;

            // ReSharper disable once LocalVariableHidesMember
            var transform = this.transform;
            if ((transform.localPosition - lastLocalPos).sqrMagnitude > POS_EPSILON
                || (transform.localScale - lastLocalScale).sqrMagnitude > POS_EPSILON
                || Quaternion.Angle(lastLocalRot, transform.localRotation) > ROT_EPSILON)
            {
                if (lockBasePosition) transform.position = correspondingObject.position;
                else correspondingObject.localPosition = transform.localPosition;

                correspondingObject.localScale = transform.localScale;
                correspondingObject.localRotation = transform.localRotation;
            }
            else
            {
                if (lockBasePosition) transform.position = correspondingObject.position;
                else transform.localPosition = correspondingObject.localPosition;
                transform.localScale = correspondingObject.localScale;
                transform.localRotation = correspondingObject.localRotation;
            }

            lastLocalPos = transform.localPosition;
            lastLocalScale = transform.localScale;
            lastLocalRot = transform.localRotation;
        }
    }
}