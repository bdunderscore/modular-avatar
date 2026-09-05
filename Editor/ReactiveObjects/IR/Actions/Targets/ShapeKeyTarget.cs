#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal readonly struct ShapeKeyTarget
    {
        public SkinnedMeshRenderer Renderer { get; }
        public string ShapeName { get; }

        public ShapeKeyTarget(SkinnedMeshRenderer renderer, string shapeName)
        {
            Renderer = renderer;
            ShapeName = shapeName;
        }

        private bool Equals(ShapeKeyTarget other)
        {
            return Equals(Renderer, other.Renderer) && ShapeName == other.ShapeName;
        }

        public override bool Equals(object? obj)
        {
            return obj is ShapeKeyTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Renderer != null ? Renderer.GetHashCode() : 0, ShapeName);
        }

        public override string ToString()
        {
            return $"{Renderer.name}:blendShape.{ShapeName}";
        }
    }
}