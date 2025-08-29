#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class RemoveVerticesFromMesh
    {
        public static Mesh RemoveVertices(Renderer renderer, Mesh original,
            IEnumerable<(TargetProp, IVertexFilter)> targets)
        {
            var mesh = new Mesh();
            mesh.indexFormat = original.indexFormat;
            mesh.bounds = original.bounds;

            bool[] toDeleteVertices = new bool[original.vertexCount];
            bool[] toRetainVertices = new bool[original.vertexCount];

            new ORFilter(targets.Select(t => t.Item2)).MarkFilteredVertices(renderer, original, toDeleteVertices);
            
            ProbeRetainedVertices(original, toDeleteVertices, toRetainVertices);

            if (toRetainVertices.All(v => !v))
            {
                // Retain vertex zero to use as a fallback
                toRetainVertices[0] = true;
                toDeleteVertices[0] = false;
            }

            RemapVerts(toRetainVertices, out var origToNewVertIndex, out var newToOrigVertIndex);

            TransferVertexData(mesh, original, toRetainVertices);
            mesh.bindposes = original.bindposes;
            TransferShapes(mesh, original, newToOrigVertIndex);
            UpdateTriangles(mesh, original, toRetainVertices, origToNewVertIndex);

            return mesh;
        }

        private static void TransferVertexData(Mesh mesh, Mesh original, bool[] toRetain)
        {
            var newToOriginal = new List<int>(toRetain.Length);

            for (var i = 0; i < toRetain.Length; i++)
            {
                if (toRetain[i])
                {
                    newToOriginal.Add(i);
                }
            }

            // We transfer all relevant attributes (positions, normals, tangents, UVs, colors, and bone weights)
            // in one go by using the raw vertex attribute stream API

            var attrs = original.GetVertexAttributes();
            mesh.SetVertexBufferParams(newToOriginal.Count, attrs);

            for (var stream = 0; stream < 4; stream++)
            {
                var stride = original.GetVertexBufferStride(stream);
                if (stride == 0) continue; // stream is not present

                var srcBuf = original.GetVertexBuffer(stream);
                var origVertexData = new byte[stride * original.vertexCount];
                srcBuf.GetData(origVertexData);

                var newVertexData = new byte[stride * newToOriginal.Count];
                for (var v = 0; v < newToOriginal.Count; v++)
                {
                    Array.Copy(origVertexData, newToOriginal[v] * stride, newVertexData, v * stride, stride);
                }

                mesh.SetVertexBufferData(newVertexData, 0, 0, newVertexData.Length, stream);
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
                var smDesc = original.GetSubMesh(sm);
                orig_tris.Clear();
                new_tris.Clear();
                orig_tris_16.Clear();
                new_tris_16.Clear();

                original.GetTriangles(orig_tris, sm, true);
                ProcessSubmesh(orig_tris, new_tris, i => i, i => i);

                if (mesh.indexFormat == IndexFormat.UInt16)
                {
                    var minVertex = Math.Max(0, new_tris.Min());

                    for (int i = 0; i < new_tris.Count; i++)
                    {
                        new_tris_16.Add((ushort)(new_tris[i] - minVertex));
                    }

                    mesh.SetIndices(new_tris_16, 0, new_tris_16.Count, smDesc.topology,
                        sm, true, minVertex);
                }
                else
                {
                    // don't bother computing min vertex for UInt32 indices, as it will always fit anyway
                    mesh.SetIndices(new_tris, 0, new_tris.Count, smDesc.topology, sm);
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
                    // Add a degenerate triangle to avoid creating an empty submesh.
                    // TODO: Perform necessary animation updates to allow us to delete the submesh entirely.
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