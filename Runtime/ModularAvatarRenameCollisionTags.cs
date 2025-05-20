#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core
{
  [Serializable]
  public struct RenameCollisionTagConfig
  {
    public string name;

    public readonly bool Equals(RenameCollisionTagConfig other)
    {
      return name == other.name;
    }

    public override readonly bool Equals(object obj)
    {
      return obj is RenameCollisionTagConfig other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
      return name != null ? name.GetHashCode() : 0;
    }
  }

  [DisallowMultipleComponent]
  [AddComponentMenu("Modular Avatar/MA Rename Collision Tags")]
  [HelpURL("https://modular-avatar.nadena.dev/docs/reference/rename-collision-tags?lang=auto")]
  public class ModularAvatarRenameCollisionTags : AvatarTagComponent
  {
    public List<RenameCollisionTagConfig> configs = new();

    public override void ResolveReferences()
    {
      // no-op
    }
  }
}
