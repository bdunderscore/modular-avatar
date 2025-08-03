using System;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IVertexFilter : IEquatable<IVertexFilter>
    {
        void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            var referenceSpace = renderer.transform;
            if (renderer is SkinnedMeshRenderer smr && smr.rootBone != null)
            {
                referenceSpace = smr.rootBone;
            }

            MarkFilteredVertices(referenceSpace, mesh, filtered);
        }

        void MarkFilteredVertices(Transform referenceSpace, Mesh mesh, bool[] filtered);

        void Observe(ComputeContext context)
        {
        }
    }
}