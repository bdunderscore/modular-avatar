using System;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IVertexFilter : IEquatable<IVertexFilter>
    {
        /// <summary>
        ///     Sets filtered[i] to true for each vertex in the mesh that is selected by this IVertexFilter.
        ///     Entries corresponding to unmatched vertices are left as-is.
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="mesh"></param>
        /// <param name="filtered"></param>
        void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered);

        void Observe(ComputeContext context)
        {
        }
    }
}