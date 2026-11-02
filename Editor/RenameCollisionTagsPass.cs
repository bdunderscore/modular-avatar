#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;

namespace nadena.dev.modular_avatar.core.editor
{
  internal class CollisionTagRenameMappings
  {
    internal Dictionary<ModularAvatarRenameVRChatCollisionTags, Dictionary<string, string>> ComponentMaps = new();

    internal bool TryGetMap(GameObject obj, out Dictionary<string, string> map)
    {
      map = null;
      var controllingComponent = obj.GetComponent<ModularAvatarRenameVRChatCollisionTags>()
                                 ?? obj.GetComponentInParent<ModularAvatarRenameVRChatCollisionTags>();
      return controllingComponent != null && ComponentMaps.TryGetValue(controllingComponent, out map);
    }
  }

  [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
  internal class RenameCollisionTagsPass : Pass<RenameCollisionTagsPass>
  {
    internal void TestExecute(ndmf.BuildContext context)
    {
      Execute(context);
    }

    private Dictionary<ModularAvatarRenameVRChatCollisionTags, Dictionary<string, string>> BuildRenameMap(
      ndmf.BuildContext context
    )
    {
      var componentMap = context.GetState<CollisionTagRenameMappings>().ComponentMaps;
      componentMap.Clear();

      // Traverses in hierarchy order
      foreach (var renamer in context.AvatarRootObject
                 .GetComponentsInChildren<ModularAvatarRenameVRChatCollisionTags>(true))
      {
        BuildReport.ReportingObject(renamer, () =>
        {
          var renameMap = new Dictionary<string, string>();
          var directParent = renamer.transform.parent?.GetComponentInParent<ModularAvatarRenameVRChatCollisionTags>();
          Dictionary<string, string> parentMap;
          if (directParent == null || !componentMap.TryGetValue(directParent, out parentMap))
          {
            // If we don't find this, it means the parent was likely located above the avatar root and should be ignored
            parentMap = new Dictionary<string, string>();
          }

          foreach (var kv in parentMap)
          {
            renameMap[kv.Key] = kv.Value;
          }

          // We're simulating allowing the incoming tags to be evaluated top-to-bottom - so when building an overall map,
          // we evaluate bottom-to-top
          foreach (var config in Enumerable.Reverse(renamer.configs))
          {
            string renameTo;
            if (config.autoRename)
            {
              renameTo = config.name + "$" + GUID.Generate();
            }
            else if (string.IsNullOrEmpty(config.renameTo))
            {
              continue;
            }
            else if (!renameMap.TryGetValue(config.renameTo, out renameTo))
            {
              renameTo = config.renameTo;
            }

            renameMap[config.name] = renameTo;
          }

          componentMap[renamer] = renameMap;
        });
      }

      return componentMap;
    }
    
    protected override void Execute(ndmf.BuildContext context)
    {
      var avatarRoot = context.AvatarRootObject;
      var contactToRenameMap = BuildRenameMap(context);

      foreach (var contact in avatarRoot.GetComponentsInChildren<ContactBase>(true))
      {
        BuildReport.ReportingObject(contact, () =>
        {
          var controllingComponent = contact.GetComponent<ModularAvatarRenameVRChatCollisionTags>()
                                     ?? contact.GetComponentInParent<ModularAvatarRenameVRChatCollisionTags>();
          if (controllingComponent == null) return;
          if (!contactToRenameMap.TryGetValue(controllingComponent, out var map)) return;

          contact.collisionTags = contact.collisionTags
            .Select(t => map.GetValueOrDefault(t) ?? t)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();
        });
      }
    }

  }
}

#endif
