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
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf.animator;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BoneDatabase
    {
        private Dictionary<Transform, bool> m_IsRetargetable = new Dictionary<Transform, bool>();

        internal void ResetBones()
        {
            m_IsRetargetable.Clear();
        }

        internal bool IsRetargetable(Transform t)
        {
            return m_IsRetargetable.TryGetValue(t, out var result) && result;
        }

        internal void AddMergedBone(Transform bone)
        {
            m_IsRetargetable[bone] = true;
        }

        internal void RetainMergedBone(Transform bone)
        {
            if (bone == null) return;
            if (m_IsRetargetable.ContainsKey(bone)) m_IsRetargetable[bone] = false;
        }

        internal Transform GetRetargetedBone(Transform bone)
        {
            if (bone == null || !m_IsRetargetable.ContainsKey(bone)) return null;

            while (bone != null && m_IsRetargetable.ContainsKey(bone) && m_IsRetargetable[bone]) bone = bone.parent;

            if (m_IsRetargetable.ContainsKey(bone)) return null;
            return bone;
        }

        internal IEnumerable<KeyValuePair<Transform, Transform>> GetRetargetedBones()
        {
            return m_IsRetargetable.Where((kvp) => kvp.Value)
                .Select(kvp => new KeyValuePair<Transform, Transform>(kvp.Key, GetRetargetedBone(kvp.Key)))
                .Where(kvp => kvp.Value != null);
        }

        public Transform GetRetargetedBone(Transform bone, bool fallbackToOriginal)
        {
            Transform retargeted = GetRetargetedBone(bone);

            return retargeted ? retargeted : (fallbackToOriginal ? bone : null);
        }
    }

    internal class RetargetMeshes
    {
        private BoneDatabase _boneDatabase;
        private AnimationIndex _animationIndex;
        private ObjectPathRemapper _pathRemapper;

        internal void OnPreprocessAvatar(GameObject avatarGameObject, BoneDatabase boneDatabase,
            AnimatorServicesContext pathMappings)
        {
            this._boneDatabase = boneDatabase;
            this._animationIndex = pathMappings.AnimationIndex;
            this._pathRemapper = pathMappings.ObjectPathRemapper;

            foreach (var renderer in avatarGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                BuildReport.ReportingObject(renderer, () =>
                {
                    bool isRetargetable = false;
                    foreach (var bone in renderer.bones)
                    {
                        if (_boneDatabase.GetRetargetedBone(bone) != null)
                        {
                            isRetargetable = true;
                            break;
                        }
                    }

                    isRetargetable |= _boneDatabase.GetRetargetedBone(renderer.rootBone);

                    if (isRetargetable)
                    {
                        new MeshRetargeter(renderer, _boneDatabase).Retarget();
                    }
                });
            }

            // Now remove retargeted bones
            if (true)
            {
                foreach (var bonePair in _boneDatabase.GetRetargetedBones())
                {
                    if (_boneDatabase.GetRetargetedBone(bonePair.Key) == null) continue;

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

                    // Remap any animation clips that reference this bone into its parent
                    _pathRemapper.ReplaceObject(sourceBone.gameObject, sourceBone.transform.parent.gameObject);
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
        private readonly BoneDatabase _boneDatabase;

        [CanBeNull] private Mesh src, dst;

        public MeshRetargeter(SkinnedMeshRenderer renderer, BoneDatabase boneDatabase)
        {
            this.renderer = renderer;
            this._boneDatabase = boneDatabase;
        }

        [CanBeNull]
        public Mesh Retarget()
        {
            var avatarTransform = RuntimeUtil.FindAvatarTransformInParents(renderer.transform);
            if (avatarTransform == null) throw new System.Exception("Could not find avatar in parents of " + renderer.name);

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
                dst.name = "RETARGETED__" + src.name;
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
                Transform newBindTarget = _boneDatabase.GetRetargetedBone(originalBones[i]);
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
                scaleBone = renderer.transform;
            }

            renderer.bones = newBones;
            if (dst)
            {
                dst.bindposes = newBindPoses;
                renderer.sharedMesh = dst;
            }

            var newRootBone = _boneDatabase.GetRetargetedBone(rootBone, true);
            if (newRootBone == null)
            {
                newRootBone = renderer.transform;
            }

            var oldLossyScale = scaleBone.transform.lossyScale;
            var newLossyScale = newRootBone.transform.lossyScale;

            var bounds = renderer.localBounds;
            bounds.extents = new Vector3(
                bounds.extents.x * oldLossyScale.x / newLossyScale.x,
                bounds.extents.y * oldLossyScale.y / newLossyScale.y,
                bounds.extents.z * oldLossyScale.z / newLossyScale.z
            );
            bounds.center = newRootBone.transform.InverseTransformPoint(
                scaleBone.transform.TransformPoint(bounds.center)
            );
            renderer.localBounds = bounds;

            renderer.rootBone = newRootBone;
            renderer.probeAnchor = _boneDatabase.GetRetargetedBone(renderer.probeAnchor, true);
        }
    }
}