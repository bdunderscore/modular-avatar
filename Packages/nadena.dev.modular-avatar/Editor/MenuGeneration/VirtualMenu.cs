using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor.menu
{
    /// <summary>
    /// Sentinel object to represent the avatar root menu (for avatars which don't have a root menu)
    /// </summary>
    internal sealed class RootMenu
    {
        public static readonly RootMenu Instance = new RootMenu();

        private RootMenu()
        {
        }
    }

    /// <summary>
    /// A MenuNode represents a single VRCExpressionsMenu, prior to overflow splitting. MenuNodes form a directed graph,
    /// which may contain cycles, and may include contributions from multiple MenuInstallers, or from the base avatar
    /// menu.
    /// </summary>
    internal class MenuNode
    {
        internal List<VirtualControl> Controls = new List<VirtualControl>();

        /// <summary>
        /// The primary (serialized) object that contributed to this menu; if we want to add more items to it, we look
        /// here. This can currently be either a VRCExpressionsMenu, a MAMenuItem, or a RootMenu.
        /// </summary>
        internal readonly object NodeKey;

        internal MenuNode(object nodeKey)
        {
            NodeKey = nodeKey;
        }
    }

    internal class VirtualControl : VRCExpressionsMenu.Control
    {
        /// <summary>
        /// VirtualControls do not reference real VRCExpressionsMenu objects, but rather virtual MenuNodes.
        /// </summary>
        internal MenuNode SubmenuNode;

        internal VirtualControl(VRCExpressionsMenu.Control control)
        {
            this.name = control.name;
            this.type = control.type;
            this.parameter = new Parameter() {name = control.parameter.name};
            this.value = control.value;
            this.icon = control.icon;
            this.style = control.style;
            this.subMenu = null;
            this.subParameters = control.subParameters.Select(p => new VRCExpressionsMenu.Control.Parameter()
            {
                name = p.name
            }).ToArray();
            this.labels = control.labels.ToArray();
        }
    }

    /**
     * The VirtualMenu class tracks a fully realized shadow menu. Notably, this is _not_ converted to unity
     * ScriptableObjects, making it easier to discard it when we need to update it.
     */
    internal class VirtualMenu
    {
        private readonly object RootMenuKey;

        /// <summary>
        /// Indexes which menu installers are contributing to which VRCExpressionMenu assets.
        /// </summary>
        private Dictionary<object, List<ModularAvatarMenuInstaller>> _targetMenuToInstaller
            = new Dictionary<object, List<ModularAvatarMenuInstaller>>();

        private Dictionary<ModularAvatarMenuInstaller, List<ModularAvatarMenuInstallTarget>> _installerToTargetComponent
            = new Dictionary<ModularAvatarMenuInstaller, List<ModularAvatarMenuInstallTarget>>();

        /// <summary>
        /// Maps from either VRCEXpressionsMenu objects or MenuItems to menu nodes. The ROOT_MENU here is a special
        /// object used to mark contributors to the avatar root menu.
        /// </summary>
        private Dictionary<object, MenuNode> _menuNodeMap = new Dictionary<object, MenuNode>();

        private Dictionary<object, MenuNode> _resolvedMenu = new Dictionary<object, MenuNode>();

        // TODO: immutable?
        public Dictionary<object, MenuNode> ResolvedMenu => _resolvedMenu;

        /// <summary>
        /// Initializes the VirtualMenu.
        /// </summary>
        /// <param name="rootMenu">The root VRCExpressionsMenu to import</param>
        internal VirtualMenu(VRCExpressionsMenu rootMenu)
        {
            if (rootMenu != null)
            {
                RootMenuKey = rootMenu;
                ImportMenu(rootMenu);
            }
            else
            {
                RootMenuKey = RootMenu.Instance;
                _menuNodeMap[RootMenu.Instance] = new MenuNode(RootMenu.Instance);
            }
        }

        private MenuNode ImportMenu(VRCExpressionsMenu menu, object menuKey = null)
        {
            if (menuKey == null) menuKey = menu;
            if (_menuNodeMap.TryGetValue(menuKey, out var subMenuNode)) return subMenuNode;

            var node = new MenuNode(menuKey);
            _menuNodeMap[menuKey] = node;
            foreach (var control in menu.controls)
            {
                var virtualControl = new VirtualControl(control);
                if (control.subMenu != null)
                {
                    virtualControl.SubmenuNode = ImportMenu(control.subMenu);
                }

                node.Controls.Add(virtualControl);
            }

            return node;
        }

        /// <summary>
        /// Registers a menu installer with this virtual menu. Because we need the full set of components indexed to
        /// determine the effects of this menu installer, further processing is deferred until we freeze the menu.
        /// </summary>
        /// <param name="installer"></param>
        internal void RegisterMenuInstaller(ModularAvatarMenuInstaller installer)
        {
            // initial validation
            if (installer.menuToAppend == null && installer.GetComponent<MenuSource>() == null) return;

            var target = installer.installTargetMenu ? (object) installer.installTargetMenu : RootMenuKey;
            if (!_targetMenuToInstaller.TryGetValue(target, out var targets))
            {
                targets = new List<ModularAvatarMenuInstaller>();
                _targetMenuToInstaller[target] = targets;
            }

            targets.Add(installer);
        }

        /// <summary>
        /// Registers an install target with this virtual menu. As with menu installers, processing is delayed.
        /// </summary>
        /// <param name="target"></param>
        internal void RegisterMenuInstallTarget(ModularAvatarMenuInstallTarget target)
        {
            if (target.installer == null) return;
            if (!_installerToTargetComponent.TryGetValue(target.installer, out var targets))
            {
                targets = new List<ModularAvatarMenuInstallTarget>();
                _installerToTargetComponent[target.installer] = targets;
            }
        }

        /// <summary>
        /// Freezes the menu, fully resolving all members of all menus.
        /// </summary>
        internal void FreezeMenu()
        {
            ResolveNode(RootMenuKey);
        }

        private HashSet<object> _sourceTrace = null;

        private MenuNode ResolveNode(object nodeKey)
        {
            if (_resolvedMenu.TryGetValue(nodeKey, out var node)) return node;

            if (nodeKey is ModularAvatarMenuItem item)
            {
                return ResolveSubmenuItem(item);
            }

            if (nodeKey is VRCExpressionsMenu menu)
            {
                ImportMenu(menu);
            }

            if (_menuNodeMap.TryGetValue(nodeKey, out node))
            {
                _resolvedMenu[nodeKey] = node;
            }
            else
            {
                node = new MenuNode(nodeKey);
                _menuNodeMap[nodeKey] = node;
                _resolvedMenu[nodeKey] = node;
            }


            // Find any menu installers which target this node, and recursively include them.
            // Note that we're also recursing through MenuNodes, and should not consider the objects visited on
            // different submenus when cutting off cycles.
            var priorTrace = _sourceTrace;
            _sourceTrace = new HashSet<object>();
            try
            {
                // We use a stack here to maintain the expected order of elements. Consider if we have three menu
                // installers as follows:
                //  A -> root
                //  B -> root
                //  C -> A
                // We'll first push [B, A], then visit A. At this point we'll push C back on the stack, so we visit
                // [A, C, B] in the end.
                Stack<ModularAvatarMenuInstaller> installers = new Stack<ModularAvatarMenuInstaller>();
                if (_targetMenuToInstaller.TryGetValue(nodeKey, out var rootInstallers))
                {
                    foreach (var i in rootInstallers.Select(x => x).Reverse())
                    {
                        if (_installerToTargetComponent.ContainsKey(i)) continue;
                        installers.Push(i);
                    }
                }

                while (installers.Count > 0)
                {
                    var next = installers.Pop();
                    if (_sourceTrace.Contains(next)) continue;
                    _sourceTrace.Add(next);

                    BuildReport.ReportingObject(next, () => ResolveInstaller(node, next, installers));
                }

                // Resolve any submenus
                foreach (var virtualControl in node.Controls)
                {
                    if (virtualControl.SubmenuNode != null)
                    {
                        virtualControl.SubmenuNode = ResolveNode(virtualControl.SubmenuNode.NodeKey);
                    }
                }
            }
            finally
            {
                _sourceTrace = priorTrace;
            }

            return node;
        }

        private MenuNode ResolveSubmenuItem(ModularAvatarMenuItem item)
        {
            return BuildReport.ReportingObject(item, () =>
            {
                MenuNode node = new MenuNode(item);
                _resolvedMenu[item] = node;

                switch (item.MenuSource)
                {
                    case SubmenuSource.External:
                    {
                        if (item.Control.subMenu != null)
                        {
                            node.Controls = ResolveNode(item.Control.subMenu).Controls;
                        }

                        break;
                    }
                    case SubmenuSource.Children:
                    {
                        var transformRoot = item.menuSource_otherObjectChildren != null
                            ? item.menuSource_otherObjectChildren.transform
                            : item.transform;
                        foreach (Transform child in transformRoot)
                        {
                            if (!child.gameObject.activeSelf) continue;

                            var source = child.GetComponent<MenuSource>();
                            if (source == null) continue;

                            if (source is ModularAvatarMenuItem subItem)
                            {
                                var control = new VirtualControl(subItem.Control);
                                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                                {
                                    control.SubmenuNode = ResolveNode(subItem);
                                }

                                control.name = subItem.gameObject.name;
                                node.Controls.Add(control);
                            }
                            else if (source is ModularAvatarMenuInstallTarget target && target.installer != null)
                            {
                                ResolveInstaller(node, target.installer, new Stack<ModularAvatarMenuInstaller>());
                            }
                            else
                            {
                                // TODO validation
                            }
                        }

                        break;
                    }
                    default:
                        // TODO validation
                        break;
                }

                return node;
            });
        }

        private void ResolveInstaller(MenuNode node, ModularAvatarMenuInstaller installer,
            Stack<ModularAvatarMenuInstaller> installers)
        {
            if (installer == null || !installer.enabled) return;

            var menuSource = installer.GetComponent<MenuSource>();

            if (menuSource == null)
            {
                var expMenu = installer.menuToAppend;
                if (expMenu == null) return;
                var controls = expMenu.controls;
                if (controls == null) return;

                foreach (var control in controls)
                {
                    var virtualControl = new VirtualControl(control);
                    if (control.subMenu != null)
                    {
                        virtualControl.SubmenuNode = ResolveNode(control.subMenu);
                    }

                    node.Controls.Add(virtualControl);
                }

                if (_targetMenuToInstaller.TryGetValue(expMenu, out var subInstallers))
                {
                    foreach (var subInstaller in subInstallers.Select(x => x).Reverse())
                    {
                        if (_installerToTargetComponent.ContainsKey(subInstaller)) continue;
                        installers.Push(subInstaller);
                    }
                }
            }
            else if (menuSource is ModularAvatarMenuInstallTarget target)
            {
                if (target.installer != null)
                {
                    installers.Push(target.installer);
                }
            }
            else if (menuSource is ModularAvatarMenuItem item)
            {
                var virtualControl = new VirtualControl(item.Control);
                virtualControl.name = item.gameObject.name;
                node.Controls.Add(virtualControl);
                if (virtualControl.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    virtualControl.SubmenuNode = ResolveNode(item);
                }
            }
            else
            {
                BuildReport.Log(ReportLevel.Error, "virtual_menu.unknown_source_type",
                    strings: new object[] {menuSource.GetType().ToString()});
            }
        }
    }
}