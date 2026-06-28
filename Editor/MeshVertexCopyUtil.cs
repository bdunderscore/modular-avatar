using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class MeshVertexCopyUtil
    {
        /// <summary>
        ///     Copies all vertex attribute streams from <paramref name="src" /> into <paramref name="dst" />.
        ///     <paramref name="newToOrig" />[newIdx] = original vertex index. Duplicate entries (cloned vertices) are allowed.
        /// </summary>
        internal static void TransferVertexData(Mesh dst, Mesh src, int[] newToOrig)
        {
            var attrs = src.GetVertexAttributes();
            dst.SetVertexBufferParams(newToOrig.Length, attrs);

            for (var stream = 0; stream < 4; stream++)
            {
                var stride = src.GetVertexBufferStride(stream);
                if (stride == 0) continue;

                var srcBuf = src.GetVertexBuffer(stream);
                var origData = new byte[stride * src.vertexCount];
                srcBuf.GetData(origData);

                var newData = new byte[stride * newToOrig.Length];
                for (var v = 0; v < newToOrig.Length; v++)
                    Array.Copy(origData, newToOrig[v] * stride, newData, v * stride, stride);

                dst.SetVertexBufferData(newData, 0, 0, newData.Length, stream);
            }
        }

        /// <summary>
        ///     Copies all blend shape frames from <paramref name="src" /> into <paramref name="dst" />.
        ///     <paramref name="newToOrig" />[newIdx] = original vertex index. Duplicate entries are allowed.
        /// </summary>
        internal static void TransferShapes(Mesh dst, Mesh src, int[] newToOrig)
        {
            dst.ClearBlendShapes();

            var newVertCount = newToOrig.Length;
            var o_pos = new Vector3[src.vertexCount];
            var n_pos = new Vector3[newVertCount];
            var o_nrm = new Vector3[src.vertexCount];
            var n_nrm = new Vector3[newVertCount];
            var o_tan = new Vector3[src.vertexCount];
            var n_tan = new Vector3[newVertCount];

            var blendShapeCount = src.blendShapeCount;
            for (var s = 0; s < blendShapeCount; s++)
            {
                var frameCount = src.GetBlendShapeFrameCount(s);
                var shapeName = src.GetBlendShapeName(s);

                for (var f = 0; f < frameCount; f++)
                {
                    src.GetBlendShapeFrameVertices(s, f, o_pos, o_nrm, o_tan);
                    for (var i = 0; i < newVertCount; i++)
                    {
                        n_pos[i] = o_pos[newToOrig[i]];
                        n_nrm[i] = o_nrm[newToOrig[i]];
                        n_tan[i] = o_tan[newToOrig[i]];
                    }

                    var weight = src.GetBlendShapeFrameWeight(s, f);
                    dst.AddBlendShapeFrame(shapeName, weight, n_pos, n_nrm, n_tan);
                }
            }
        }
    }
}