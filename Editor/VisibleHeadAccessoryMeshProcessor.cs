using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Many avatars have hair meshes which are partially painted to the Head bone, and partially to a bone we might
    /// want to make visible in the first-person view. To handle this case, we must retarget those meshes to use our
    /// proxy head bone instead.
    /// </summary>
    internal class VisibleHeadAccessoryMeshProcessor
    {
        private SkinnedMeshRenderer _renderer;
        private HashSet<Transform> _visibleBones;
        private Transform _proxyHead;

        public VisibleHeadAccessoryMeshProcessor(
            SkinnedMeshRenderer renderer,
            HashSet<Transform> visibleBones,
            Transform proxyHead
        )
        {
            _renderer = renderer;
            _visibleBones = visibleBones;
            _proxyHead = proxyHead;
        }

        public bool NeedsRetargeting()
        {
            return _renderer.bones.Any(_visibleBones.Contains) && _renderer.bones.Any(b => !_visibleBones.Contains(b));
        }

        public void Retarget(BuildContext context)
        {
            if (!NeedsRetargeting()) return;

            bool anyVisible = false;
            var originalMesh = _renderer.sharedMesh;
            var newMesh = Object.Instantiate(originalMesh);
            context.SaveAsset(newMesh);

            // Identify the manifolds which need to be retargeted. Generally, we define a manifold as the maximal set of
            // points which are connected by primitives. If this manifold contains both visible and retargeted bones,
            // we need to retarget any retargeted bones in there.
            //
            // To do this, we use my favorite algorithm: The Union-Find set algorithm! This is one of my favorite
            // algorithms/data-structures and I'm very happy to finally have a reason to apply it. Yay!
            var vertexCount = originalMesh.vertexCount;
            ManifoldNode[] nodes = new ManifoldNode[vertexCount];
            var boneWeights = originalMesh.GetAllBoneWeights();
            var bonesPerVertex = originalMesh.GetBonesPerVertex();

            AnalyzeManifolds();
            // Bail out early if the bones are unused
            if (!anyVisible) return;

            // Now construct a new bone weight array
            var bindposes = new List<Matrix4x4>();
            originalMesh.GetBindposes(bindposes);

            var bones = new List<Transform>(_renderer.bones);
            var proxyIndices = new Dictionary<int, int>();

            var newWeights = new List<BoneWeight1>();

            int src_w_base = 0;
            for (int v = 0; v < vertexCount; v++)
            {
                var weightPerVertex = bonesPerVertex[v];

                bool remapManifold = nodes[v].HasRetargetedBone && nodes[v].HasVisibleBone;

                for (int w = 0; w < weightPerVertex; w++)
                {
                    var weight = boneWeights[src_w_base + w];
                    var bone = _renderer.bones[weight.boneIndex];

                    // Check for broken bone bindings, and if so just copy over.
                    // Also just copy over if the manifold doesn't need adjustment.
                    if (bone == null || !remapManifold || _visibleBones.Contains(bone))
                    {
                        newWeights.Add(weight);
                    }
                    else
                    {
                        // This bone needs to be remapped, so do the thing.
                        int newIndex = RemapBone(weight.boneIndex);
                        weight.boneIndex = newIndex;
                        newWeights.Add(weight);
                    }
                }

                src_w_base += weightPerVertex;
            }

            using (var nativeWeights = new NativeArray<BoneWeight1>(newWeights.ToArray(), Allocator.Temp))
            {
                newMesh.SetBoneWeights(
                    bonesPerVertex,
                    nativeWeights
                );
            }

            newMesh.bindposes = bindposes.ToArray();

            _renderer.bones = bones.ToArray();
            _renderer.sharedMesh = newMesh;

            //newMesh.colors = vcol.ToArray();

            int RemapBone(int originalIndex)
            {
                if (proxyIndices.TryGetValue(originalIndex, out var index)) return index;

                index = bones.Count;
                bones.Add(_proxyHead);

                // The original bindpose is the inverse of the transform matrix of the bone as it was in the 3D editor,
                // which does not necessarily match where the bone is right now. That is, we can imagine that some
                // additional unknown transform T has been applied on top of the bone pose B, and the bindpose is
                // therefore K = (T * B)^-1 = B^-1 * T^-1. Poses are computed as P' = B * K * P = B * B^-1 * T^-1 * P
                //
                // What we want to find is the bindpose that maps onto the proxy head transform. We can imagine that
                // unity will eventually multiply some point P by pose Q, then the head pose like so: P' = H * Q * P
                // We want to get the same result: B * K * P = H * Q * P; thus B * K = H * Q.
                // Since H, B, K are known, we can solve like so: Q = H^-1 * B * K

                var k = bindposes[originalIndex];
                var b = _renderer.bones[originalIndex].localToWorldMatrix;
                var hInv = _proxyHead.worldToLocalMatrix;

                bindposes.Add(hInv * b * k);
                proxyIndices[originalIndex] = index;

                return index;
            }

            void AnalyzeManifolds()
            {
                int boneIndex = 0;
                for (int i = 0; i < vertexCount; i++)
                {
                    nodes[i] = new ManifoldNode();

                    var weightsForVertex = bonesPerVertex[i];
                    for (int w = 0; w < weightsForVertex; w++)
                    {
                        var weight = boneWeights[boneIndex + w];
                        var bone = _renderer.bones[weight.boneIndex];
                        if (bone == null) continue;
                        if (_visibleBones.Contains(bone))
                        {
                            anyVisible = true;
                            nodes[i].HasVisibleBone = true;
                        }
                        else
                        {
                            nodes[i].HasRetargetedBone = true;
                        }
                    }

                    boneIndex += weightsForVertex;
                }

                if (!anyVisible) return;

                for (int s = 0; s < newMesh.subMeshCount; s++)
                {
                    var topology = newMesh.GetTopology(s);
                    if (topology != MeshTopology.Triangles) continue;
                    var indices = newMesh.GetIndices(s);

                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        nodes[indices[t]].Union(nodes[indices[t + 1]]);
                        nodes[indices[t]].Union(nodes[indices[t + 2]]);
                    }
                }
            }
        }


        private class ManifoldNode
        {
            private bool _hasVisibleBone, _hasRetargetedBone;
            private ManifoldNode parent;
            private int rank;
            public float b;

            public int Rank => rank;

            public bool HasVisibleBone
            {
                get => Find()._hasVisibleBone;
                set => Find()._hasVisibleBone |= value;
            }

            public bool HasRetargetedBone
            {
                get => Find()._hasRetargetedBone;
                set => Find()._hasRetargetedBone |= value;
            }

            public ManifoldNode()
            {
                _hasVisibleBone = false;
                _hasRetargetedBone = false;
                parent = this;
                rank = 1;
            }

            public ManifoldNode Find()
            {
                // Path halving algorithm
                ManifoldNode node = this;
                while (node.parent != node)
                {
                    node.parent = node.parent.parent;
                    node = node.parent;
                }

                return node;
            }

            internal void Union(ManifoldNode other)
            {
                var x = Find();
                var y = other.Find();

                if (x == y) return;
                if (x.rank < y.rank)
                {
                    var tmp = x;
                    x = y;
                    y = tmp;
                }

                y.parent = x;
                x.rank += y.rank;

                if (x._hasRetargetedBone != y._hasRetargetedBone)
                {
                    //Debug.Log("!");
                }

                x._hasRetargetedBone |= y._hasRetargetedBone;
                x._hasVisibleBone |= y._hasVisibleBone;
            }
        }
    }
}