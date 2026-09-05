#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal readonly struct MaterialSlotTarget
    {
        public Renderer Renderer { get; }
        public int MaterialIndex { get; }

        public MaterialSlotTarget(Renderer renderer, int materialIndex)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
        }

        private bool Equals(MaterialSlotTarget other)
        {
            return Equals(Renderer, other.Renderer) && MaterialIndex == other.MaterialIndex;
        }

        public override bool Equals(object? obj)
        {
            return obj is MaterialSlotTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Renderer != null ? Renderer.GetHashCode() : 0, MaterialIndex);
        }

        public override string ToString()
        {
            return $"{Renderer.name}:m_Materials.Array.data[{MaterialIndex}]";
        }
    }
}