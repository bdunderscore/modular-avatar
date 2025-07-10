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
    public bool autoRename;
    public string renameTo;
  }

  [DisallowMultipleComponent]
  [AddComponentMenu("Modular Avatar/MA Rename VRChat Collision Tags")]
  [HelpURL("https://modular-avatar.nadena.dev/docs/reference/rename-collision-tags?lang=auto")]
  public class ModularAvatarRenameVRChatCollisionTags : AvatarTagComponent
  {
    public List<RenameCollisionTagConfig> configs = new();

    public override void ResolveReferences()
    {
      // no-op
    }
  }
}
