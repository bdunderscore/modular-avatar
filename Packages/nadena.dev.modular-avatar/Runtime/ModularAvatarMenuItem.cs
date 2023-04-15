using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.menu;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    public enum SubmenuSource
    {
        MenuAsset,
        Children,
    }

    [AddComponentMenu("Modular Avatar/MA Menu Item")]
    public class ModularAvatarMenuItem : ActionController, MenuSource
    {
        public VRCExpressionsMenu.Control Control;
        public SubmenuSource MenuSource;

        public GameObject menuSource_otherObjectChildren;

        [FormerlySerializedAs("toggleGroup")] public ControlGroup controlGroup;

        /// <summary>
        /// If no control group is set (and an action is linked), this controls whether this control is synced.
        /// </summary>
        public bool isSynced = true;

        public bool isSaved = true;

        internal override bool isSyncedProp => isSynced;
        internal override bool isSavedProp => isSaved;

        protected override void OnValidate()
        {
            base.OnValidate();

            RuntimeUtil.InvalidateMenu();

            if (Control == null)
            {
                Control = new VRCExpressionsMenu.Control();
            }
        }

        public void Visit(NodeContext context)
        {
            if (Control == null)
            {
                Control = new VRCExpressionsMenu.Control();
            }

            var cloned = new VirtualControl(Control);
            cloned.subMenu = null;
            cloned.name = gameObject.name;

            if (cloned.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                switch (this.MenuSource)
                {
                    case SubmenuSource.MenuAsset:
                        cloned.SubmenuNode = context.NodeFor(this.Control.subMenu);
                        break;
                    case SubmenuSource.Children:
                    {
                        var root = this.menuSource_otherObjectChildren != null
                            ? this.menuSource_otherObjectChildren
                            : this.gameObject;

                        cloned.SubmenuNode = context.NodeFor(new MenuNodesUnder(root));
                        break;
                    }
                }
            }

            context.PushControl(cloned);
        }
    }
}