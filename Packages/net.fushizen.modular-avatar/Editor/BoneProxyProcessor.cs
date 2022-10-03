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

namespace net.fushizen.modular_avatar.core.editor
{
    internal class BoneProxyProcessor
    {
        internal void OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var boneProxies = avatarGameObject.GetComponentsInChildren<ModularAvatarBoneProxy>(true);

            foreach (var proxy in boneProxies)
            {
                if (proxy.target != null)
                {
                    var oldPath = RuntimeUtil.AvatarRootPath(proxy.gameObject);
                    Transform transform = proxy.transform;
                    transform.SetParent(proxy.target, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    PathMappings.Remap(oldPath, new PathMappings.MappingEntry()
                    {
                        path = RuntimeUtil.AvatarRootPath(proxy.gameObject),
                        transformPath = RuntimeUtil.AvatarRootPath(proxy.gameObject)
                    });
                }

                Object.DestroyImmediate(proxy);
            }
        }
    }
}