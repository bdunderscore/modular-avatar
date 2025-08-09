using System;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IVertexFilter : IEquatable<IVertexFilter>
    {
        void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered);

        void Observe(ComputeContext context)
        {
        }
    }
}