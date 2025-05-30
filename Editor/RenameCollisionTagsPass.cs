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

      // renameCollisionTags ごとに GUID を管理
      var guidMap = new Dictionary<ModularAvatarRenameVRChatCollisionTags, string>();

      foreach (var contact in contacts)
      {
        BuildReport.ReportingObject(contact, () =>
        {
          // Contact の親方向に最も近い ModularAvatarRenameVRChatCollisionTags を取得
          var renameCollisionTags = contactToRenameMap[contact];
          if (renameCollisionTags == null) return;

          // renameCollisionTags ごとの GUID を生成または取得
          if (!guidMap.TryGetValue(renameCollisionTags, out var guid))
          {
            guid = GUID.Generate().ToString();
            guidMap[renameCollisionTags] = guid;
          }

          var matchedTags = renameCollisionTags.configs.Select(x => x.name).Intersect(contact.collisionTags).ToHashSet();
          if (matchedTags.Count == 0) return;

          foreach (var tag in matchedTags)
          {
            var index = contact.collisionTags.IndexOf(tag);
            string newTag = $"{tag}${guid}";
            contact.collisionTags[index] = newTag;
          }
        });
      }
    }

  }
}

#endif
