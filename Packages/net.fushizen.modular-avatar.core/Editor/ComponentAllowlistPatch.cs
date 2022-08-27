using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using VRC.SDK3.Validation;

namespace net.fushizen.modular_avatar.core.editor
{
    [InitializeOnLoad]
    internal static class ComponentAllowlistPatch
    {
        static ComponentAllowlistPatch()
        {
            var listField = typeof(AvatarValidation).GetField(nameof(AvatarValidation.ComponentTypeWhiteListCommon),
                BindingFlags.Static | BindingFlags.Public);
            var currentList = new List<string>(listField.GetValue(null) as string[]);
            currentList.Add(typeof(AvatarTagComponent).FullName);
            listField.SetValue(null, currentList.ToArray());
        }
    }
}