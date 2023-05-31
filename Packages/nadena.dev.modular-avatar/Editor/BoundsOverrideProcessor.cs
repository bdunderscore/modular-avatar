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
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BoundsOverrideProcessor
    {
        internal void OnProcessAvatar(GameObject avatarObject)
        {
            Queue<ModularAvatarBoundsOverride> overrides = new Queue<ModularAvatarBoundsOverride>();
            Queue<ModularAvatarBoundsOverrideBlocker> blockers = new Queue<ModularAvatarBoundsOverrideBlocker>();

            FindTopLevelOverrides(avatarObject.transform, overrides);

            while (overrides.Count > 0 || blockers.Count > 0)
            {
                while (overrides.Count > 0)
                {
                    var processTargetOverride = overrides.Dequeue();
                    BuildReport.ReportingObject(processTargetOverride,
                        () => ProcessOverride(processTargetOverride, overrides, blockers));
                }

                while (blockers.Count > 0)
                {
                    var processTargetBlocker = blockers.Dequeue();

                    foreach (Transform children in processTargetBlocker.transform.OfType<Transform>())
                    {
                        FindTopLevelOverrides(children, overrides);
                    }
                }
            }
        }

        private static void FindTopLevelOverrides(Transform root, Queue<ModularAvatarBoundsOverride> overrides)
        {
            Queue<Transform> childrenQueue = new Queue<Transform>();
            childrenQueue.Enqueue(root);

            while (childrenQueue.Count > 0)
            {
                var currentTransform = childrenQueue.Dequeue();

                var currentOverride = currentTransform.GetComponent<ModularAvatarBoundsOverride>();
                if (currentOverride != null)
                {
                    overrides.Enqueue(currentOverride);
                    continue;
                }

                foreach (Transform children in currentTransform.OfType<Transform>())
                {
                    childrenQueue.Enqueue(children);
                }
            }
        }

        private static void ProcessOverride(ModularAvatarBoundsOverride targetOverride,
            Queue<ModularAvatarBoundsOverride> overrides, Queue<ModularAvatarBoundsOverrideBlocker> blockers)
        {
            var targetRenderer = targetOverride.GetComponent<SkinnedMeshRenderer>();
            if (targetRenderer != null)
            {
                var rootBone = targetOverride.rootBoneTarget.Get(targetOverride)?.transform;
                if (rootBone != null)
                {
                    targetRenderer.rootBone = targetOverride.rootBoneTarget.Get(targetOverride)?.transform;
                }

                targetRenderer.localBounds = targetOverride.bounds;
            }

            var processTargetBlocker = targetOverride.GetComponent<ModularAvatarBoundsOverrideBlocker>();
            if (processTargetBlocker != null)
            {
                blockers.Enqueue(processTargetBlocker);
                return;
            }

            ProcessOverrideChildren(targetOverride, overrides, blockers);
        }

        private static void ProcessOverrideChildren(ModularAvatarBoundsOverride targetOverride,
            Queue<ModularAvatarBoundsOverride> overrides, Queue<ModularAvatarBoundsOverrideBlocker> blockers)
        {
            Queue<Transform> childrenQueue = new Queue<Transform>();
            foreach (Transform children in targetOverride.transform.OfType<Transform>())
            {
                childrenQueue.Enqueue(children);
            }

            while (childrenQueue.Count > 0)
            {
                var currentTransform = childrenQueue.Dequeue();

                var currentOverride = currentTransform.GetComponent<ModularAvatarBoundsOverride>();
                if (currentOverride != null)
                {
                    overrides.Enqueue(currentOverride);
                    continue;
                }

                var currentBlocker = currentTransform.GetComponent<ModularAvatarBoundsOverrideBlocker>();
                if (currentBlocker != null)
                {
                    blockers.Enqueue(currentBlocker);
                    continue;
                }

                var currentRenderer = currentTransform.GetComponent<SkinnedMeshRenderer>();
                if (currentRenderer != null)
                {
                    var rootBone = targetOverride.rootBoneTarget.Get(targetOverride)?.transform;
                    if (rootBone != null)
                    {
                        currentRenderer.rootBone = targetOverride.rootBoneTarget.Get(targetOverride)?.transform;
                    }

                    currentRenderer.localBounds = targetOverride.bounds;
                }

                foreach (Transform children in currentTransform.OfType<Transform>())
                {
                    childrenQueue.Enqueue(children);
                }
            }
        }
    }
}