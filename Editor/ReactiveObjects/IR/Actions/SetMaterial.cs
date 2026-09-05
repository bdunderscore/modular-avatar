#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class SetMaterial : IAction
    {
        public Renderer Renderer { get; }
        public int MaterialIndex { get; }
        public Material? Material { get; }

        public object TargetKey => new MaterialSlotTarget(Renderer, MaterialIndex);

        public SetMaterial(Renderer renderer, int materialIndex, Material? material)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
            Material = material;
        }

        public bool ApproximatelyEqual(IAction other)
        {
            return Equals(other);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            if (Renderer == null) return StaticApplyResult.Retain;

            var materials = Renderer.sharedMaterials;
            if (MaterialIndex < 0 || MaterialIndex >= materials.Length) return StaticApplyResult.Retain;

            materials[MaterialIndex] = Material;
            Renderer.sharedMaterials = materials;
            return StaticApplyResult.Applied;
        }

        private bool Equals(SetMaterial other)
        {
            return Equals(Renderer, other.Renderer) && MaterialIndex == other.MaterialIndex &&
                   Equals(Material, other.Material);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is SetMaterial other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Renderer != null ? Renderer.GetHashCode() : 0, MaterialIndex,
                Material != null ? Material.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return $"SetMaterial({Renderer.name}, {MaterialIndex}, {Material})";
        }
    }
}