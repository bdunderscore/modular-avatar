#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

using nadena.dev.modular_avatar.editor.ErrorReporting;
using VRC.Dynamics;

namespace nadena.dev.modular_avatar.core.editor
{
  internal class RenameCollisionTagsPass
  {

    public static void Process(GameObject avatarRoot)
    {
      var renameCollisionTagsComponents = avatarRoot.GetComponentsInChildren<ModularAvatarRenameCollisionTags>(true);

      if (renameCollisionTagsComponents.Length == 0) return;

      var contacts = avatarRoot.GetComponentsInChildren<ContactBase>(true);

      if (contacts.Length == 0) return;

      // Contacts から親を辿って最短で見つけた renameCollisionTags と紐づける
      Dictionary<Transform, List<ContactBase>> contactTagMap = new();
      var avatarTransform = avatarRoot.transform;
      foreach (var contact in contacts)
      {
        BuildReport.ReportingObject(contact, () =>
        {
          var node = contact.transform;
          while (node != null)
          {
            if (node.TryGetComponent<ModularAvatarRenameCollisionTags>(out var renameCollisionTags))
            {
              if (contact.collisionTags.Intersect(renameCollisionTags.configs.Select(x => x.name)).Any())
              {
                if (!contactTagMap.TryGetValue(node, out var list))
                {
                  list = new List<ContactBase>();
                  contactTagMap.Add(node, list);
                }
                list.Add(contact);
                break;
              }
            }

            if (node == avatarTransform) break;
            node = node.parent;
          }
        });
      }

      foreach (var renameCollisionTags in renameCollisionTagsComponents)
      {
        BuildReport.ReportingObject(renameCollisionTags, () =>
        {
          if (contactTagMap.TryGetValue(renameCollisionTags.transform, out var contactsList))
          {
            // renameCollisionTags ごとにGUIDを生成する
            string guid = GUID.Generate().ToString();
            foreach (var contact in contactsList)
            {
              var matchedTags = renameCollisionTags.configs.Select(x => x.name).Intersect(contact.collisionTags).ToHashSet();
              if (matchedTags.Count == 0) continue;

              foreach (var tag in matchedTags)
              {
                // 順序を維持したままタグを上書きする
                var index = contact.collisionTags.IndexOf(tag);
                string newTag = $"{tag}${guid}";
                contact.collisionTags[index] = newTag;
              }
            }
          }
        });
      }
    }
  }
}

#endif
