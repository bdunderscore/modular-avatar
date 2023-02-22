using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    [DisallowMultipleComponent]
    public abstract class MenuSource : AvatarTagComponent
    {
        /**
         * Generates the menu items for this menu source object. Submenus are not required to be persisted as assets;
         * this will be handled by the caller if necessary.
         *
         * Note that this method might be called outside of a build context (e.g. from custom inspectors).
         */
        internal abstract VRCExpressionsMenu.Control[] GenerateMenu();

        protected override void OnValidate()
        {
            base.OnValidate();

            RuntimeUtil.InvalidateMenu();
        }
    }


    public enum SubmenuSource
    {
        MenuAsset,
        Children,
    }

    [AddComponentMenu("Modular Avatar/MA Menu Item")]
    public class ModularAvatarMenuItem : MenuSource
    {
        public VRCExpressionsMenu.Control Control;
        public SubmenuSource MenuSource;

        public ModularAvatarMenuInstaller menuSource_installer;
        public GameObject menuSource_otherObjectChildren;

        internal override VRCExpressionsMenu.Control[] GenerateMenu()
        {
            switch (Control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    return GenerateSubmenu();
                default:
                    return new[]
                        {Control};
            }
        }

        private bool _recursing = false;
        private VRCExpressionsMenu _cachedMenu;

        private VRCExpressionsMenu.Control[] GenerateSubmenu()
        {
            List<VRCExpressionsMenu.Control> controls = null;
            switch (MenuSource)
            {
                case SubmenuSource.MenuAsset:
                    controls = Control.subMenu?.controls?.ToList();
                    break;
                case SubmenuSource.Children:
                {
                    var menuRoot = menuSource_otherObjectChildren == null
                        ? gameObject
                        : menuSource_otherObjectChildren;
                    controls = new List<VRCExpressionsMenu.Control>();
                    foreach (Transform child in menuRoot.transform)
                    {
                        var menuSource = child.GetComponent<MenuSource>();
                        if (menuSource != null && child.gameObject.activeSelf && menuSource.enabled)
                        {
                            controls.AddRange(menuSource.GenerateMenu());
                        }
                    }

                    break;
                }
                /*
                case SubmenuSource.MenuInstaller:
                    controls = menuSource_installer.installTargetMenu?.controls?.ToList();
                    break;
                case SubmenuSource.OtherMenuItem:
                    if (_recursing || menuSource_otherSource == null)
                    {
                        return new VRCExpressionsMenu.Control[] { };
                    }
                    else
                    {
                        _recursing = true;
                        try
                        {
                            return menuSource_otherSource.GenerateMenu();
                        }
                        finally
                        {
                            _recursing = false;
                        }
                    }
                    */
            }

            if (controls == null)
            {
                return new VRCExpressionsMenu.Control[] { };
            }

            if (_cachedMenu == null) _cachedMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            _cachedMenu.controls = controls;

            var control = CloneControl(Control);
            control.name = gameObject.name;
            control.subMenu = _cachedMenu;

            return new[] {control};
        }

        private static VRCExpressionsMenu.Control CloneControl(VRCExpressionsMenu.Control control)
        {
            return new VRCExpressionsMenu.Control()
            {
                type = control.type,
                parameter = control.parameter,
                labels = control.labels.ToArray(),
                subParameters = control.subParameters.ToArray(),
                icon = control.icon,
                name = control.name,
                value = control.value,
                subMenu = control.subMenu
            };
        }
    }
}