#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class SetShapeKey : IAction
    {
        public SkinnedMeshRenderer Renderer { get; }
        public string ShapeName { get; }
        public float Value { get; }

        public object TargetKey => new ShapeKeyTarget(Renderer, ShapeName);

        public SetShapeKey(SkinnedMeshRenderer renderer, string shapeName, float value)
        {
            Renderer = renderer;
            ShapeName = shapeName;
            Value = value;
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return other is SetShapeKey shape && Equals(Renderer, shape.Renderer) && ShapeName == shape.ShapeName &&
                   Math.Abs(Value - shape.Value) <= 0.001f;
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            if (Renderer == null || Renderer.sharedMesh == null) return StaticApplyResult.Retain;

            var index = Renderer.sharedMesh.GetBlendShapeIndex(ShapeName);
            if (index < 0) return StaticApplyResult.Retain;

            Renderer.SetBlendShapeWeight(index, Value);
            return StaticApplyResult.Applied;
        }

        private bool Equals(SetShapeKey other)
        {
            return Equals(Renderer, other.Renderer) && ShapeName == other.ShapeName && Value.Equals(other.Value);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is SetShapeKey other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Renderer != null ? Renderer.GetHashCode() : 0, ShapeName, Value);
        }

        public override string ToString()
        {
            return $"SetShapeKey({Renderer.name}, {ShapeName}, {Value})";
        }
    }
}