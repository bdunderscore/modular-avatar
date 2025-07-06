using System;
using System.Collections.Immutable;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class MaterialOverride : IEquatable<MaterialOverride>
    {
        public readonly Material Material = null;
        public readonly ImmutableDictionary<string, Texture> TextureOverrides = ImmutableDictionary<string, Texture>.Empty;

        public MaterialOverride(Material material, ImmutableDictionary<string, Texture> textureOverrides)
        {
            Material = material;
            TextureOverrides = textureOverrides ?? ImmutableDictionary<string, Texture>.Empty;
        }

        public bool Equals(MaterialOverride other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (ReferenceEquals(other, null)) return false;
            if (Material != other.Material) return false;
            if (TextureOverrides.Count != other.TextureOverrides.Count) return false;
            foreach (var (name, texture) in TextureOverrides)
            {
                if (!other.TextureOverrides.TryGetValue(name, out var otherTexture)) return false;
                if (texture != otherTexture) return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MaterialOverride);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Material);
            foreach (var (name, texture) in TextureOverrides)
            {
                hash = HashCode.Combine(hash, name, texture);
            }
            return hash;
        }

        public Material ToMaterial()
        {
            var material = new Material(Material);
            foreach (var (name, texture) in TextureOverrides)
            {
                material.SetTexture(name, texture);
            }
            return material;
        }
    }
}
