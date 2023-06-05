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
using JetBrains.Annotations;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class BoneDatabase
    {
        private static Dictionary<Transform, bool> IsRetargetable = new Dictionary<Transform, bool>();

        internal static void ResetBones()
        {
            IsRetargetable.Clear();
        }

        internal static void AddMergedBone(Transform bone)
        {
            IsRetargetable[bone] = true;
        }

        internal static void RetainMergedBone(Transform bone)
        {
            if (bone == null) return;
            if (IsRetargetable.ContainsKey(bone)) IsRetargetable[bone] = false;
        }

        internal static Transform GetRetargetedBone(Transform bone)
        {
            if (bone == null || !IsRetargetable.ContainsKey(bone)) return null;

            while (bone != null && IsRetargetable.ContainsKey(bone) && IsRetargetable[bone]) bone = bone.parent;

            if (IsRetargetable.ContainsKey(bone)) return null;
            return bone;
        }

        internal static IEnumerable<KeyValuePair<Transform, Transform>> GetRetargetedBones()
        {
            return IsRetargetable.Where((kvp) => kvp.Value)
                .Select(kvp => new KeyValuePair<Transform, Transform>(kvp.Key, GetRetargetedBone(kvp.Key)))
                .Where(kvp => kvp.Value != null);
        }

        public static Transform GetRetargetedBone(Transform bone, bool fallbackToOriginal)
        {
            Transform retargeted = GetRetargetedBone(bone);

            return retargeted ? retargeted : (fallbackToOriginal ? bone : null);
        }
    }

    internal class RetargetMeshes
    {
        private BuildContext _context;

        internal void OnPreprocessAvatar(GameObject avatarGameObject, BuildContext context)
        {
            _context = context;

            foreach (var renderer in avatarGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                BuildReport.ReportingObject(renderer, () =>
                {
                    bool isRetargetable = false;
                    foreach (var bone in renderer.bones)
                    {
                        if (BoneDatabase.GetRetargetedBone(bone) != null)
                        {
                            isRetargetable = true;
                            break;
                        }
                    }

                    isRetargetable |= BoneDatabase.GetRetargetedBone(renderer.rootBone);

                    if (isRetargetable)
                    {
                        var newMesh = new MeshRetargeter(renderer).Retarget();
                        if (newMesh) _context.SaveAsset(newMesh);
                    }
                });
            }

            // Now remove retargeted bones
            if (true)
            {
                foreach (var bonePair in BoneDatabase.GetRetargetedBones())
                {
                    if (BoneDatabase.GetRetargetedBone(bonePair.Key) == null) continue;

                    var sourceBone = bonePair.Key;
                    var destBone = bonePair.Value;

                    // Check that we don't have any components left over (e.g. Unity colliders) that need to stick
                    // around.
                    var components = sourceBone.GetComponents<Component>();
                    bool has_unknown_component = false;
                    foreach (var component in components)
                    {
                        if (component is Transform || component is AvatarTagComponent)
                        {
                            continue; // we assume MA components are okay to purge by this point
                        }

                        has_unknown_component = true;
                        break;
                    }

                    if (has_unknown_component) continue;

                    var children = new List<Transform>();
                    foreach (Transform child in sourceBone)
                    {
                        children.Add(child);
                    }

                    foreach (Transform child in children)
                    {
                        child.SetParent(destBone, true);
                    }

                    PathMappings.MarkRemoved(sourceBone.gameObject);
                    UnityEngine.Object.DestroyImmediate(sourceBone.gameObject);
                }
            }
        }
    }

    /**
     * This class processes a given mesh, adjusting the bind poses for any bones that are to be merged to instead match
     * the bind pose of the original avatar's bone.
     */
    internal class MeshRetargeter
    {
        private readonly SkinnedMeshRenderer renderer;
        [CanBeNull] private Mesh src, dst;

        public MeshRetargeter(SkinnedMeshRenderer renderer)
        {
            this.renderer = renderer;
        }

        [CanBeNull]
        public Mesh Retarget()
        {
            var avatar = RuntimeUtil.FindAvatarInParents(renderer.transform);
            if (avatar == null) throw new System.Exception("Could not find avatar in parents of " + renderer.name);
            var avatarTransform = avatar.transform;

            var avPos = avatarTransform.position;
            var avRot = avatarTransform.rotation;
            var avScale = avatarTransform.lossyScale;

            avatarTransform.position = Vector3.zero;
            avatarTransform.rotation = Quaternion.identity;
            avatarTransform.localScale = Vector3.one;

            src = renderer.sharedMesh;
            if (src != null)
            {
                dst = Mesh.Instantiate(src);
                dst.name = "RETARGETED: " + src.name;
            }

            RetargetBones();
            AdjustShapeKeys();

            avatarTransform.position = avPos;
            avatarTransform.rotation = avRot;
            avatarTransform.localScale = avScale;

            return dst;
        }

        private void AdjustShapeKeys()
        {
            // TODO
        }

        private void RetargetBones()
        {
            var originalBindPoses = src ? src.bindposes : null;
            var originalBones = renderer.bones;

            var newBones = (Transform[]) originalBones.Clone();
            var newBindPoses = (Matrix4x4[]) originalBindPoses?.Clone();

            for (int i = 0; i < originalBones.Length; i++)
            {
                Transform newBindTarget = BoneDatabase.GetRetargetedBone(originalBones[i]);
                if (newBindTarget == null) continue;
                newBones[i] = newBindTarget;

                if (originalBindPoses != null)
                {
                    Matrix4x4 Bp = newBindTarget.worldToLocalMatrix * originalBones[i].localToWorldMatrix *
                                   originalBindPoses[i];

                    newBindPoses[i] = Bp;
                }
            }

            var rootBone = renderer.rootBone;
            var scaleBone = rootBone;
            if (rootBone == null)
            {
                // Sometimes meshes have no root bone set. This is usually not ideal, but let's make sure we don't
                // choke on the scale computation below.
                scaleBone = renderer.bones[0];
            }

            renderer.bones = newBones;
            if (dst)
            {
                dst.bindposes = newBindPoses;
                renderer.sharedMesh = dst;
            }

            var newRootBone = BoneDatabase.GetRetargetedBone(rootBone, true);
            var newScaleBone = BoneDatabase.GetRetargetedBone(scaleBone, true);

            var oldLossyScale = scaleBone.transform.lossyScale;
            var newLossyScale = newScaleBone.transform.lossyScale;

            var bounds = renderer.localBounds;
            bounds.extents = new Vector3(
                bounds.extents.x * oldLossyScale.x / newLossyScale.x,
                bounds.extents.y * oldLossyScale.y / newLossyScale.y,
                bounds.extents.z * oldLossyScale.z / newLossyScale.z
            );
            bounds.center = new Vector3(
                bounds.center.x * oldLossyScale.x / newLossyScale.x,
                bounds.center.y * oldLossyScale.y / newLossyScale.y,
                bounds.center.z * oldLossyScale.z / newLossyScale.z
            );
            renderer.localBounds = bounds;

            renderer.rootBone = newRootBone;
            renderer.probeAnchor = BoneDatabase.GetRetargetedBone(renderer.probeAnchor, true);
        }
    }
}