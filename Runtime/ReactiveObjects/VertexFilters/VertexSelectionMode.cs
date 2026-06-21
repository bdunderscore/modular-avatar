using System;
using JetBrains.Annotations;

namespace nadena.dev.modular_avatar.core.vertex_filters
{
    [Serializable]
    [PublicAPI]
    public enum VertexSelectionMode
    {
        AnyVertex,
        AllVertices,
        Centroid
    }
}