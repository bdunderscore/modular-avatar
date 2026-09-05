#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal readonly struct MeshSectionTarget
    {
        private enum TargetKind
        {
            Shape,
            Mask
        }

        private readonly TargetKind _kind;

        public SkinnedMeshRenderer Renderer { get; }
        public string? ShapeName { get; }
        public IMeshSelector? Selector { get; }
        public bool IsShape => _kind == TargetKind.Shape;

        private MeshSectionTarget(SkinnedMeshRenderer renderer, TargetKind kind, string? shapeName,
            IMeshSelector? selector)
        {
            Renderer = renderer;
            _kind = kind;
            ShapeName = shapeName;
            Selector = selector;
        }

        public static MeshSectionTarget ForShape(SkinnedMeshRenderer renderer, string shapeName)
        {
            return new MeshSectionTarget(renderer, TargetKind.Shape, shapeName, null);
        }

        public static MeshSectionTarget ForMask(SkinnedMeshRenderer renderer, IMeshSelector selector)
        {
            return new MeshSectionTarget(renderer, TargetKind.Mask, null, selector);
        }

        private bool Equals(MeshSectionTarget other)
        {
            return _kind == other._kind && Equals(Renderer, other.Renderer) &&
                   (_kind == TargetKind.Shape
                       ? ShapeName == other.ShapeName
                       : Selector?.Equals(other.Selector) is true);
        }

        public override bool Equals(object? obj)
        {
            return obj is MeshSectionTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)_kind, Renderer != null ? Renderer.GetHashCode() : 0,
                // false positive
                // ReSharper disable once RedundantCast
                _kind == TargetKind.Shape ? (object?)ShapeName : Selector);
        }

        public override string ToString()
        {
            return _kind == TargetKind.Shape
                ? $"{Renderer.name}:shape-delete:{ShapeName}"
                : $"{Renderer.name}:mesh-section:{Selector}";
        }
    }
}