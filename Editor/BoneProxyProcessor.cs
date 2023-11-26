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

using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BoneProxyProcessor
    {
        internal enum ValidationResult
        {
            OK,
            MovingTarget,
            NotInAvatar
        }

        internal void OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var boneProxies = avatarGameObject.GetComponentsInChildren<ModularAvatarBoneProxy>(true);

            foreach (var proxy in boneProxies)
            {
                BuildReport.ReportingObject(proxy, () => ProcessProxy(avatarGameObject, proxy));
            }
        }

        private void ProcessProxy(GameObject avatarGameObject, ModularAvatarBoneProxy proxy)
        {
            if (proxy.target != null && ValidateTarget(avatarGameObject, proxy.target) == ValidationResult.OK)
            {
                string suffix = "";
                int i = 1;
                while (proxy.target.Find(proxy.gameObject.name + suffix) != null)
                {
                    suffix = $" ({i++})";
                }

                proxy.gameObject.name += suffix;

                Transform transform = proxy.transform;
                transform.SetParent(proxy.target, true);

                bool keepPos, keepRot;
                switch (proxy.attachmentMode)
                {
                    default:
                    case BoneProxyAttachmentMode.Unset:
                    case BoneProxyAttachmentMode.AsChildAtRoot:
                        keepPos = keepRot = false;
                        break;
                    case BoneProxyAttachmentMode.AsChildKeepWorldPose:
                        keepPos = keepRot = true;
                        break;
                    case BoneProxyAttachmentMode.AsChildKeepPosition:
                        keepPos = true;
                        keepRot = false;
                        break;
                    case BoneProxyAttachmentMode.AsChildKeepRotation:
                        keepRot = true;
                        keepPos = false;
                        break;
                }

                if (!keepPos) transform.localPosition = Vector3.zero;
                if (!keepRot) transform.localRotation = Quaternion.identity;
            }

            Object.DestroyImmediate(proxy);
        }

        internal static ValidationResult ValidateTarget(GameObject avatarGameObject, Transform proxyTarget)
        {
            var avatar = avatarGameObject.transform;
            var node = proxyTarget;

            while (node != null && node != avatar)
            {
                if (node.GetComponent<ModularAvatarMergeArmature>() != null ||
                    node.GetComponent<ModularAvatarBoneProxy>() != null)
                {
                    return ValidationResult.MovingTarget;
                }

                node = node.parent;
            }

            if (node == null) return ValidationResult.NotInAvatar;
            else return ValidationResult.OK;
        }
    }
}