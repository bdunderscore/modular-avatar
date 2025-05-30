#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Linq;
using UnityEditor;

using VRC.Dynamics;

using nadena.dev.ndmf;
using nadena.dev.modular_avatar.editor.ErrorReporting;

namespace nadena.dev.modular_avatar.core.editor
{

  [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
  internal class RenameCollisionTagsPass : Pass<RenameCollisionTagsPass>
  {
    internal void TestExecute(ndmf.BuildContext context)
    {
      Execute(context);
    }
    protected override void Execute(ndmf.BuildContext context)
    {
      var avatarRoot = context.AvatarRootObject;

      var contacts = avatarRoot.GetComponentsInChildren<ContactBase>(true);
      if (contacts.Length == 0) return;

      var contactToRenameMap = contacts.ToDictionary(
        contact => contact,
        contact => contact.GetComponentInParent<ModularAvatarRenameVRChatCollisionTags>()
      );

      // keep track of guid values for each ModularAvatarRenameVRChatCollisionTags component
      var guidMap = new Dictionary<ModularAvatarRenameVRChatCollisionTags, string>();

      foreach (var contact in contacts)
      {
        BuildReport.ReportingObject(contact, () =>
        {
          var renameCollisionTags = contactToRenameMap[contact];
          if (renameCollisionTags == null) return;

          var newCollisionTags = contact.collisionTags.Select(tag =>
          {
            var configs = renameCollisionTags.configs.Where(config => config.name == tag);
            if (configs.Count() == 0) return tag;
            var config = configs.First();

            if (config.autoRename)
            {
              if (!guidMap.TryGetValue(renameCollisionTags, out var guid))
              {
                guid = GUID.Generate().ToString();
                guidMap[renameCollisionTags] = guid;
              }
              return $"{tag}${guid}";
            }

            if (!string.IsNullOrEmpty(config.renameTo))
            {
              return config.renameTo;
            }

            return tag;
          }).ToList();

          contact.collisionTags = newCollisionTags;
        });
      }
    }

  }
}

#endif
