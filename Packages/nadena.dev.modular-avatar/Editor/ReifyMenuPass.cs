using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReifyMenuPass
    {
        public void OnPreprocessAvatar(VRCAvatarDescriptor root, BuildContext context)
        {
            foreach (ModularAvatarMenuInstaller installer in
                     root.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                BuildReport.ReportingObject(installer, () => ReifyMenu(context, installer));
            }
        }

        private void ReifyMenu(BuildContext context, ModularAvatarMenuInstaller installer)
        {
            var source = installer.GetComponent<MenuSource>();
            if (source == null) return;

            var controls = source.GenerateMenu();
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = controls.ToList();

            installer.menuToAppend = context.CloneMenu(menu);
        }
    }
}