#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class RemoveBlendShapeFromMesh
    {
        private const float THRESHOLD = 0.001f;

        public static Mesh RemoveBlendshapes(Mesh original, IEnumerable<int> targets)
        {
            var mesh = new Mesh();
            mesh.indexFormat = original.indexFormat;
            mesh.bounds = original.bounds;

            bool[] toDeleteVertices = new bool[original.vertexCount];
            bool[] toRetainVertices = new bool[original.vertexCount];

            ProbeBlendshapes(original, toDeleteVertices, targets);
            ProbeRetainedVertices(original, toDeleteVertices, toRetainVertices);

            RemapVerts(toRetainVertices, out var origToNewVertIndex, out var newToOrigVertIndex);

            TransferVertexData(mesh, original, toRetainVertices);
            TransferBoneData(mesh, original, toRetainVertices);
            mesh.bindposes = original.bindposes;
            TransferShapes(mesh, original, newToOrigVertIndex);
            UpdateTriangles(mesh, original, toRetainVertices, origToNewVertIndex);

            return mesh;
        }


        private static VertexAttribute[] uvAttrs = new[]
        {
            VertexAttribute.TexCoord0,
            VertexAttribute.TexCoord1,
            VertexAttribute.TexCoord2,
            VertexAttribute.TexCoord3,
            VertexAttribute.TexCoord4,
            VertexAttribute.TexCoord5,
            VertexAttribute.TexCoord6,
            VertexAttribute.TexCoord7,
        };

        private static void TransferVertexData(Mesh mesh, Mesh original, bool[] toRetain)
        {
            List<Vector2> tmpVec2 = new();
            List<Vector3> tmpVec3 = new();
            List<Vector4> tmpVec4 = new();

            TransferData(tmpVec3, mesh.SetVertices, original.GetVertices);
            TransferData(tmpVec3, mesh.SetNormals, original.GetNormals);
            TransferData(tmpVec4, mesh.SetTangents, original.GetTangents);

            for (int uv = 0; uv < 8; uv++)
            {
                if (!original.HasVertexAttribute(uvAttrs[uv])) continue;
                switch (original.GetVertexAttributeDimension(uvAttrs[uv]))
                {
                    case 2:
                        TransferData(tmpVec2, l => mesh.SetUVs(uv, l), l => original.GetUVs(uv, l));
                        break;
                    case 3:
                        TransferData(tmpVec3, l => mesh.SetUVs(uv, l), l => original.GetUVs(uv, l));
                        break;
                    case 4:
                        TransferData(tmpVec4, l => mesh.SetUVs(uv, l), l => original.GetUVs(uv, l));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            void TransferData<T>(List<T> tmp, Action<List<T>> setter, Action<List<T>> getter)
            {
                getter(tmp);
                int index = 0;

                tmp.RemoveAll(_t => !toRetain[index++]);

                setter(tmp);
            }
        }

        private static void TransferShapes(Mesh mesh, Mesh original, int[] newToOrigVertIndex)
        {
            Vector3[] o_pos = new Vector3[original.vertexCount];
            Vector3[] n_pos = new Vector3[mesh.vertexCount];

            Vector3[] o_nrm = new Vector3[original.vertexCount];
            Vector3[] n_nrm = new Vector3[mesh.vertexCount];

            Vector3[] o_tan = new Vector3[original.vertexCount];
            Vector3[] n_tan = new Vector3[mesh.vertexCount];

            int blendshapeCount = original.blendShapeCount;
            for (int s = 0; s < blendshapeCount; s++)
            {
                int frameCount = original.GetBlendShapeFrameCount(s);
                var shapeName = original.GetBlendShapeName(s);

                for (int f = 0; f < frameCount; f++)
                {
                    original.GetBlendShapeFrameVertices(s, f, o_pos, o_nrm, o_tan);
                    Remap();

                    var frameWeight = original.GetBlendShapeFrameWeight(s, f);
                    mesh.AddBlendShapeFrame(shapeName, frameWeight, n_pos, n_nrm, n_tan);
                }
            }

            void Remap()
            {
                for (int i = 0; i < n_pos.Length; i++)
                {
                    try
                    {
                        n_pos[i] = o_pos[newToOrigVertIndex[i]];
                        n_nrm[i] = o_nrm[newToOrigVertIndex[i]];
                        n_tan[i] = o_tan[newToOrigVertIndex[i]];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw;
                    }
                }
            }
        }

        private static void TransferBoneData(Mesh mesh, Mesh original, bool[] toRetain)
        {
            var origBoneWeights = original.GetAllBoneWeights();
            var origBonesPerVertex = original.GetBonesPerVertex();

            List<BoneWeight1> boneWeights = new(origBoneWeights.Length);
            List<byte> bonesPerVertex = new(origBonesPerVertex.Length);

            if (origBonesPerVertex.Length == 0) return; // no bones in this mesh

            int ptr = 0;
            for (int i = 0; i < toRetain.Length; i++)
            {
                byte n_weight = origBonesPerVertex[i];

                if (toRetain[i])
                {
                    for (int j = 0; j < n_weight; j++)
                    {
                        boneWeights.Add(origBoneWeights[ptr + j]);
                    }

                    bonesPerVertex.Add(n_weight);
                }

                ptr += n_weight;
            }

            var native_boneWeights = new NativeArray<BoneWeight1>(boneWeights.ToArray(), Allocator.Temp);
            var native_bonesPerVertex = new NativeArray<byte>(bonesPerVertex.ToArray(), Allocator.Temp);

            mesh.SetBoneWeights(native_bonesPerVertex, native_boneWeights);

            native_boneWeights.Dispose();
            native_bonesPerVertex.Dispose();
        }

        private static void UpdateTriangles(Mesh mesh, Mesh original, bool[] toRetainVertices, int[] origToNewVertIndex)
        {
            int submeshCount = original.subMeshCount;

            List<int> orig_tris = new List<int>();
            List<int> new_tris = new List<int>();

            List<ushort> orig_tris_16 = new List<ushort>();
            List<ushort> new_tris_16 = new List<ushort>();

            mesh.subMeshCount = submeshCount;

            for (int sm = 0; sm < submeshCount; sm++)
            {
                if (original.indexFormat == IndexFormat.UInt32)
                {
                    original.GetTriangles(orig_tris, sm, true);
                    ProcessSubmesh<int>(orig_tris, new_tris, i => i, i => i);

                    int min = Math.Max(0, new_tris.Min());
                    for (int i = 0; i < new_tris.Count; i++)
                    {
                        new_tris[i] -= min;
                    }

                    mesh.SetTriangles(new_tris, sm, true, min);
                }
                else
                {
                    original.GetTriangles(orig_tris_16, sm, true);
                    ProcessSubmesh<ushort>(orig_tris_16, new_tris_16, i => i, i => (ushort)i);

                    ushort min = new_tris_16.Min();
                    for (int i = 0; i < new_tris_16.Count; i++)
                    {
                        new_tris_16[i] -= min;
                    }

                    mesh.SetTriangles(new_tris_16, sm, true, min);
                }
            }

            void ProcessSubmesh<T>(List<T> orig_tri, List<T> new_tri, Func<T, int> toInt, Func<int, T> fromInt)
            {
                int limit = orig_tri.Count - 2;

                new_tri.Clear();

                for (int i = 0; i < limit; i += 3)
                {
                    if (!toRetainVertices[toInt(orig_tri[i])]
                        || !toRetainVertices[toInt(orig_tri[i + 1])]
                        || !toRetainVertices[toInt(orig_tri[i + 2])]
                       )
                    {
                        continue;
                    }

                    new_tri.Add(fromInt(origToNewVertIndex[toInt(orig_tri[i])]));
                    new_tri.Add(fromInt(origToNewVertIndex[toInt(orig_tri[i + 1])]));
                    new_tri.Add(fromInt(origToNewVertIndex[toInt(orig_tri[i + 2])]));
                }

                if (new_tri.Count == 0)
                {
                    new_tri.Add(default);
                    new_tri.Add(default);
                    new_tri.Add(default);
                }
            }
        }

        private static void RemapVerts(bool[] toRetainVertices, out int[] origToNewVertIndex,
            out int[] newToOrigVertIndex)
        {
            List<int> n2o = new List<int>(toRetainVertices.Length);
            List<int> o2n = new List<int>(toRetainVertices.Length);

            for (int j = 0; j < toRetainVertices.Length; j++)
            {
                if (toRetainVertices[j])
                {
                    o2n.Add(n2o.Count);
                    n2o.Add(j);
                }
                else
                {
                    o2n.Add(-1);
                }
            }

            newToOrigVertIndex = n2o.ToArray();
            origToNewVertIndex = o2n.ToArray();
        }

        private static void ProbeBlendshapes(Mesh mesh, bool[] toDeleteVertices, IEnumerable<int> shapes)
        {
            var bsPos = new Vector3[mesh.vertexCount];

            foreach (var index in shapes)
            {
                int frames = mesh.GetBlendShapeFrameCount(index);

                for (int f = 0; f < frames; f++)
                {
                    mesh.GetBlendShapeFrameVertices(index, f, bsPos, null, null);

                    for (int i = 0; i < bsPos.Length; i++)
                    {
                        if (bsPos[i].sqrMagnitude > 0.0001f)
                        {
                            toDeleteVertices[i] = true;
                        }
                    }
                }
            }
        }

        private static void ProbeRetainedVertices(Mesh mesh, bool[] toDeleteVertices, bool[] toRetainVertices)
        {
            List<int> tris = new List<int>();
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                tris.Clear();

                var baseVertex = (int)mesh.GetBaseVertex(subMesh);
                mesh.GetTriangles(tris, subMesh, false);

                for (int i = 0; i < tris.Count; i += 3)
                {
                    if (toDeleteVertices[tris[i] + baseVertex] || toDeleteVertices[tris[i + 1] + baseVertex] ||
                        toDeleteVertices[tris[i + 2] + baseVertex])
                    {
                        continue;
                    }
                    else
                    {
                        toRetainVertices[tris[i] + baseVertex] = true;
                        toRetainVertices[tris[i + 1] + baseVertex] = true;
                        toRetainVertices[tris[i + 2] + baseVertex] = true;
                    }
                }
            }
        }
    }
}