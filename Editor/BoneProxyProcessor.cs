#nullable enable

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

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BoneProxyState
    {
        public readonly Dictionary<ModularAvatarBoneProxy, GameObject?> TargetMapping = new();
    }

    internal class BoneProxyPluginPrepass : Pass<BoneProxyPluginPrepass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var state = context.GetState<BoneProxyState>();
            foreach (var bp in context.AvatarRootObject.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
            {
                state.TargetMapping[bp] = bp.target?.gameObject;
            }
        }

        internal void ExecuteForTesting(ndmf.BuildContext context)
        {
            Execute(context);
        }
    }

    internal class BoneProxyPluginPass : Pass<BoneProxyPluginPass>
    {
        internal enum ValidationResult
        {
            OK,
            MovingTarget,
            NotInAvatar
        }

        private class ProxyInfo
        {
            public readonly ModularAvatarBoneProxy Proxy;
            public readonly Transform Target;
            public readonly Vector3 WorldPos;
            public readonly Quaternion WorldRot;

            public ProxyInfo(ModularAvatarBoneProxy proxy, BoneProxyState state)
            {
                Proxy = proxy;
                Target = state.TargetMapping.GetValueOrDefault(proxy)?.transform ?? proxy.target; 
                WorldPos = proxy.transform.position;
                WorldRot = proxy.transform.rotation;
            }

            internal void AdjustTransform()
            {
                bool keepPos, keepRot;
                switch (Proxy.attachmentMode)
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

                var transform = Proxy.transform;
                if (keepPos)
                {
                    transform.position = WorldPos;
                }
                else
                {
                    transform.localPosition = Vector3.zero;
                }

                if (keepRot)
                {
                    transform.rotation = WorldRot;
                }
                else
                {
                    transform.localRotation = Quaternion.identity;
                }

                if (Proxy.matchScale)
                {
                    transform.localScale = Vector3.one;
                }
            }
        }

        protected override void Execute(ndmf.BuildContext context)
        {
            var avatarGameObject = context.AvatarRootObject;
            var state = context.GetState<BoneProxyState>();
            
            var boneProxies = avatarGameObject.GetComponentsInChildren<ModularAvatarBoneProxy>(true)
                .Select(bp => new ProxyInfo(bp, state))
                .ToList();

            foreach (var proxy in boneProxies)
            {
                BuildReport.ReportingObject(proxy.Proxy, () => ProcessProxy(avatarGameObject, proxy));
            }

            // Process parent to child to ensure keep-world-position is handled properly
            foreach (var proxy in boneProxies.OrderBy(p => RuntimeUtil.AvatarRootPath(p.Proxy.gameObject)))
            {
                BuildReport.ReportingObject(proxy.Proxy, () => proxy.AdjustTransform());
            }

            // Clean up the bone proxies now that we're done making corrections
            foreach (var proxy in boneProxies)
            {
                Object.DestroyImmediate(proxy.Proxy);
            }
        }

        private void ProcessProxy(GameObject avatarGameObject, ProxyInfo proxy)
        {
            if (proxy.Target != null && ValidateTarget(avatarGameObject, proxy.Target) == ValidationResult.OK)
            {
                string suffix = "";
                int i = 1;
                while (proxy.Target.Find(proxy.Proxy.gameObject.name + suffix) != null)
                {
                    suffix = $" ({i++})";
                }

                proxy.Proxy.gameObject.name += suffix;

                var transform = proxy.Proxy.transform;
                transform.SetParent(proxy.Target, true);
            }
        }

        internal void ExecuteForTesting(ndmf.BuildContext context)
        {
            Execute(context);
        }

        internal static ValidationResult ValidateTarget(GameObject avatarGameObject, Transform proxyTarget)
        {
            var avatar = avatarGameObject.transform;
            var node = proxyTarget;

            while (node != null && node != avatar)
            {
                node = node.parent;
            }

            if (node == null) return ValidationResult.NotInAvatar;
            else return ValidationResult.OK;
        }
    }
}