#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

// Internal runtime API for the Virtual Menu system.
//
// IMPORTANT: This API is currently considered unstable. Due to C# protection rules, we are required to make classes
// here public, but be aware that they may change without warning in the future.
namespace nadena.dev.modular_avatar.core.menu
{
    /// <summary>
    /// A MenuNode represents a single VRCExpressionsMenu, prior to overflow splitting. MenuNodes form a directed graph,
    /// which may contain cycles, and may include contributions from multiple MenuInstallers, or from the base avatar
    /// menu.
    /// </summary>
    public class VirtualMenuNode
    {
        public List<VirtualControl> Controls = new List<VirtualControl>();

        /// <summary>
        /// The primary (serialized) object that contributed to this menu; if we want to add more items to it, we look
        /// here. This can currently be either a VRCExpressionsMenu, a MAMenuItem, or a RootMenu.
        /// </summary>
        public readonly object NodeKey;

        internal VirtualMenuNode(object nodeKey)
        {
            NodeKey = nodeKey;
        }
    }

    /**
     * A single control on a MenuNode. The main difference between this and a true VRCExpressionsMenu.Control is that
     * we use a MenuNode instead of a VRCExpressionsMenu for submenus.
     */
    public class VirtualControl : VRCExpressionsMenu.Control
    {
        /// <summary>
        /// VirtualControls do not reference real VRCExpressionsMenu objects, but rather virtual MenuNodes.
        /// </summary>
        public VirtualMenuNode SubmenuNode;

        internal VirtualControl(VRCExpressionsMenu.Control control)
        {
            this.name = control.name;
            this.type = control.type;
            this.parameter = new Parameter() {name = control?.parameter?.name};
            this.value = control.value;
            this.icon = control.icon;
            this.style = control.style;
            this.subMenu = null;
            this.subParameters = control.subParameters?.Select(p => new VRCExpressionsMenu.Control.Parameter()
            {
                name = p.name
            })?.ToArray() ?? Array.Empty<Parameter>();
            this.labels = control.labels?.ToArray() ?? Array.Empty<Label>();
        }
    }

    /// <summary>
    /// Helper MenuSource which includes all children of a given GameObject containing MenuSourceComponents as menu
    /// items. Implements equality based on the GameObject in question.
    /// </summary>
    internal class MenuNodesUnder : MenuSource
    {
        internal readonly GameObject root;

        public MenuNodesUnder(GameObject root)
        {
            this.root = root;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return root == ((MenuNodesUnder) obj).root;
        }

        public override int GetHashCode()
        {
            return (root != null ? root.GetHashCode() : 0);
        }

        public void Visit(NodeContext context)
        {
            foreach (Transform t in root.transform)
            {
                var source = t.GetComponent<MenuSource>();
                if (source != null) context.PushNode(source);
            }
        }
    }

    /// <summary>
    /// The NodeContext provides callbacks for MenuSource visitors to append controls and/or other node types to a menu
    /// node.
    /// </summary>
    public interface NodeContext
    {
        /// <summary>
        /// Pushes the contents of this expressions menu asset onto the current menu node, handling loops and menu
        /// installer invocations.
        /// </summary>
        /// <param name="expMenu"></param>
        void PushMenuContents(VRCExpressionsMenu expMenu);

        /// <summary>
        /// Pushes the contents of this menu source onto the current menu node.
        /// </summary>
        /// <param name="source"></param>
        void PushNode(MenuSource source);

        /// <summary>
        /// Pushes this menu installer onto this node
        /// </summary>
        /// <param name="installer"></param>
        void PushNode(ModularAvatarMenuInstaller installer);

        /// <summary>
        /// Pushes a single expressions menu control onto the current menu node. Converts submenus into menu nodes
        /// automatically.
        /// </summary>
        /// <param name="control"></param>
        void PushControl(VRCExpressionsMenu.Control control);

        /// <summary>
        /// Pushes a single expressions menu control onto the current menu node.
        /// </summary>
        /// <param name="control"></param>
        void PushControl(VirtualControl control);

        /// <summary>
        /// Returns the menu node for a given VRCExpressionsMenu asset. This node may not be populated at the time this
        /// node returns.
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        VirtualMenuNode NodeFor(VRCExpressionsMenu menu);

        /// <summary>
        /// Returns the menu node for a given menu source asset. The contents of the node may not yet be populated.
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        VirtualMenuNode NodeFor(MenuSource menu);
    }

    /// <summary>
    /// An object which can contribute controls to a menu.
    /// </summary>
    public interface MenuSource
    {
        void Visit(NodeContext context);
    }

    /// <summary>
    /// A component which can be used to generate menu items.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MenuSourceComponent : AvatarTagComponent, MenuSource
    {
        protected override void OnValidate()
        {
            base.OnValidate();

            RuntimeUtil.InvalidateMenu();
        }

        public abstract void Visit(NodeContext context);
    }
}

#endif