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

using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class PhysboneBlockerPass
    {
        public static void Process(GameObject avatarRoot)
        {
            var blockers = avatarRoot.GetComponentsInChildren<ModularAvatarPBBlocker>(true);
            if (blockers.Length == 0) return;

            var physBones = avatarRoot.GetComponentsInChildren<VRCPhysBone>(true);
            if (physBones.Length == 0) return;

            Dictionary<Transform, List<Transform>> physBoneRootToIgnores = new Dictionary<Transform, List<Transform>>();

            var avatarTransform = avatarRoot.transform;
            foreach (var tip in blockers)
            {
                BuildReport.ReportingObject(tip, () =>
                {
                    var node = tip.transform;
                    // We deliberately skip the node itself to allow for a specific PhysBone to be attached here.
                    while (node != null && node != avatarTransform && node.parent != null)
                    {
                        node = node.parent;
                        if (!physBoneRootToIgnores.TryGetValue(node, out var parent))
                        {
                            parent = new List<Transform>();
                            physBoneRootToIgnores.Add(node, parent);
                        }

                        parent.Add(tip.transform);
                    }
                });
            }

            foreach (var pb in physBones)
            {
                BuildReport.ReportingObject(pb, () =>
                {
                    var root = pb.rootTransform != null ? pb.rootTransform : pb.transform;
                    if (physBoneRootToIgnores.TryGetValue(root, out var ignores))
                    {
                        pb.ignoreTransforms.AddRange(ignores);
                    }
                });
            }
        }
    }
}

#endif