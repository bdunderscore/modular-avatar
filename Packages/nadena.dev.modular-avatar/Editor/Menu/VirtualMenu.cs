using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.core.menu;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor.menu
{
    /// <summary>
    /// Sentinel object to represent the avatar root menu (for avatars which don't have a root menu)
    /// </summary>
    internal sealed class RootMenu : MenuSource
    {
        public static readonly RootMenu Instance = new RootMenu();

        private RootMenu()
        {
        }

        public void Visit(NodeContext context)
        {
            // we initialize the root node manually
            throw new NotImplementedException();
        }
    }

    class NodeContextImpl : NodeContext
    {
        [CanBeNull]
        internal delegate VirtualMenuNode NodeForDelegate(object menu);

        private readonly ImmutableDictionary<object, ImmutableList<ModularAvatarMenuInstaller>>
            _menuToInstallerMap;

        private readonly ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>
            _postProcessControls
                = ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>.Empty;

        private readonly VirtualMenuNode _node;
        private readonly NodeForDelegate _nodeFor;
        private readonly Action<VRCExpressionsMenu> _visitedMenu;
        private readonly HashSet<object> _visited = new HashSet<object>();

        private Action<VRCExpressionsMenu.Control> _currentPostprocessor = _control => { };

        private class PostprocessorContext : IDisposable
        {
            private NodeContextImpl _context;
            private Action<VRCExpressionsMenu.Control> _priorPreprocessor;

            public PostprocessorContext(NodeContextImpl context, Action<VRCExpressionsMenu.Control> preprocessor)
            {
                this._context = context;
                this._priorPreprocessor = context._currentPostprocessor;
                context._currentPostprocessor = preprocessor;
            }

            public void Dispose()
            {
                _context._currentPostprocessor = _priorPreprocessor;
            }
        }

        public NodeContextImpl(
            VirtualMenuNode node,
            NodeForDelegate nodeFor,
            ImmutableDictionary<object, ImmutableList<ModularAvatarMenuInstaller>> menuToInstallerMap,
            ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>> postProcessControls,
            Action<VRCExpressionsMenu> visitedMenu
        )
        {
            _node = node;
            _nodeFor = nodeFor;
            _menuToInstallerMap = menuToInstallerMap;
            _postProcessControls = postProcessControls;
            _visitedMenu = visitedMenu;
        }

        public void PushNode(VRCExpressionsMenu expMenu)
        {
            if (expMenu == null) return;
            if (_visited.Contains(expMenu)) return;
            _visited.Add(expMenu);
            _visitedMenu(expMenu);

            foreach (var control in expMenu.controls)
            {
                PushControl(control);
            }

            if (_menuToInstallerMap.TryGetValue(expMenu, out var installers))
            {
                foreach (var installer in installers)
                {
                    using (new PostprocessorContext(this, null))
                    {
                        PushNode(installer);
                    }
                }
            }
        }

        public void PushNode(MenuSource source)
        {
            if (source == null) return;
            if (_visited.Contains(source)) return;
            _visited.Add(source);

            BuildReport.ReportingObject(source as UnityEngine.Object, () => source.Visit(this));
        }

        public void PushNode(ModularAvatarMenuInstaller installer)
        {
            if (installer == null) return;
            if (_visited.Contains(installer)) return;
            _visited.Add(installer);

            BuildReport.ReportingObject(installer, () =>
            {
                var menuSourceComp = installer.GetComponent<MenuSource>();
                if (menuSourceComp != null)
                {
                    PushNode(menuSourceComp);
                }
                else if (installer.menuToAppend != null)
                {
                    using (new PostprocessorContext(this, _postProcessControls.GetValueOrDefault(installer)))
                    {
                        PushNode(installer.menuToAppend);
                    }
                }
            });
        }

        public void PushControl(VRCExpressionsMenu.Control control)
        {
            var virtualControl = new VirtualControl(control);

            virtualControl.SubmenuNode = NodeFor(control.subMenu);

            PushControl(virtualControl);
        }

        public void PushControl(VirtualControl control)
        {
            _node.Controls.Add(control);
        }

        public VirtualMenuNode NodeFor(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            return _nodeFor(menu);
        }

        public VirtualMenuNode NodeFor(MenuSource source)
        {
            if (source == null) return null;
            return _nodeFor(source);
        }
    }

    /**
     * The VirtualMenu class tracks a fully realized shadow menu. Notably, this is _not_ converted to unity
     * ScriptableObjects, making it easier to discard it when we need to update it.
     */
    internal class VirtualMenu
    {
        internal readonly object RootMenuKey;

        private static long _cacheSeq = 0;

        internal static void InvalidateCaches()
        {
            _cacheSeq++;
        }

        static VirtualMenu()
        {
            RuntimeUtil.OnMenuInvalidate += InvalidateCaches;
        }

        internal static long CacheSequence => _cacheSeq;

        private readonly long _initialCacheSeq = _cacheSeq;
        internal bool IsOutdated => _initialCacheSeq != _cacheSeq;

        /// <summary>
        /// Indexes which menu installers are contributing to which VRCExpressionMenu assets.
        /// </summary>
        private Dictionary<object, List<ModularAvatarMenuInstaller>> _targetMenuToInstaller
            = new Dictionary<object, List<ModularAvatarMenuInstaller>>();

        private Dictionary<ModularAvatarMenuInstaller, List<ModularAvatarMenuInstallTarget>> _installerToTargetComponent
            = new Dictionary<ModularAvatarMenuInstaller, List<ModularAvatarMenuInstallTarget>>();

        private ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>
            _postprocessControlsHooks =
                ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>.Empty;

        private Dictionary<object, VirtualMenuNode> _resolvedMenu = new Dictionary<object, VirtualMenuNode>();

        // TODO: immutable?
        public Dictionary<object, VirtualMenuNode> ResolvedMenu => _resolvedMenu;
        public VirtualMenuNode RootMenuNode => ResolvedMenu[RootMenuKey];

        private Queue<Action> _pendingGeneration = new Queue<Action>();
        private HashSet<VRCExpressionsMenu> _visitedMenus = new HashSet<VRCExpressionsMenu>();

        /// <summary>
        /// Initializes the VirtualMenu.
        /// </summary>
        /// <param name="rootMenu">The root VRCExpressionsMenu to import</param>
        internal VirtualMenu(
            VRCExpressionsMenu rootMenu,
            BuildContext context = null
        )
        {
            if (context != null)
            {
                _postprocessControlsHooks = context.PostProcessControls.ToImmutableDictionary();
            }

            if (rootMenu != null)
            {
                RootMenuKey = rootMenu;
            }
            else
            {
                RootMenuKey = RootMenu.Instance;
            }
        }

        internal static VirtualMenu ForAvatar(
            VRCAvatarDescriptor avatar,
            BuildContext context = null
        )
        {
            var menu = new VirtualMenu(avatar.expressionsMenu, context);
            foreach (var installer in avatar.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                menu.RegisterMenuInstaller(installer);
            }

            foreach (var target in avatar.GetComponentsInChildren<ModularAvatarMenuInstallTarget>(true))
            {
                menu.RegisterMenuInstallTarget(target);
            }

            menu.FreezeMenu();

            return menu;
        }

        internal IEnumerable<ModularAvatarMenuInstallTarget> GetInstallTargetsForInstaller(
            ModularAvatarMenuInstaller installer
        )
        {
            if (_installerToTargetComponent.TryGetValue(installer, out var targets))
            {
                return targets;
            }
            else
            {
                return Array.Empty<ModularAvatarMenuInstallTarget>();
            }
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

            targets.Add(target);
        }

        /// <summary>
        /// Freezes the menu, fully resolving all members of all menus.
        /// </summary>
        internal void FreezeMenu()
        {
            ImmutableDictionary<object, ImmutableList<ModularAvatarMenuInstaller>> menuToInstallerFiltered =
                _targetMenuToInstaller
                    .Select(kvp => new KeyValuePair<object, ImmutableList<ModularAvatarMenuInstaller>>(
                        kvp.Key,
                        kvp.Value.Where(i => !_installerToTargetComponent.ContainsKey(i)).ToImmutableList()
                    ))
                    .Where(kvp => !kvp.Value.IsEmpty)
                    .ToImmutableDictionary();

            var RootNode = new VirtualMenuNode(RootMenuKey);
            _resolvedMenu[RootMenuKey] = RootNode;

            var rootContext =
                new NodeContextImpl(RootNode, NodeFor, menuToInstallerFiltered, _postprocessControlsHooks,
                    m => _visitedMenus.Add(m));
            if (RootMenuKey is VRCExpressionsMenu menu)
            {
                foreach (var control in menu.controls)
                {
                    rootContext.PushControl(control);
                }
            }

            if (menuToInstallerFiltered.TryGetValue(RootMenuKey, out var installers))
            {
                foreach (var installer in installers)
                {
                    rootContext.PushNode(installer);
                }
            }

            while (_pendingGeneration.Count > 0)
            {
                _pendingGeneration.Dequeue()();
            }

            VirtualMenuNode NodeFor(object key)
            {
                if (_resolvedMenu.TryGetValue(key, out var node)) return node;
                node = new VirtualMenuNode(key);
                _resolvedMenu[key] = node;

                _pendingGeneration.Enqueue(() =>
                {
                    BuildReport.ReportingObject(key as UnityEngine.Object, () =>
                    {
                        var context = new NodeContextImpl(node, NodeFor, menuToInstallerFiltered,
                            _postprocessControlsHooks,
                            m => _visitedMenus.Add(m));
                        if (key is VRCExpressionsMenu expMenu)
                        {
                            context.PushNode(expMenu);
                        }
                        else if (key is MenuSource source)
                        {
                            context.PushNode(source);
                        }
                        else
                        {
                            // TODO warning
                        }
                    });
                });

                return node;
            }
        }

        internal VRCExpressionsMenu SerializeMenu(Action<UnityEngine.Object> SaveAsset)
        {
            Dictionary<object, VRCExpressionsMenu> serializedMenus = new Dictionary<object, VRCExpressionsMenu>();

            return Serialize(RootMenuKey);

            VRCExpressionsMenu Serialize(object menuKey)
            {
                if (menuKey == null) return null;
                if (serializedMenus.TryGetValue(menuKey, out var menu)) return menu;
                if (!_resolvedMenu.TryGetValue(menuKey, out var node)) return null;

                menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                serializedMenus[menuKey] = menu;
                menu.controls = node.Controls.Select(c =>
                {
                    var control = new VRCExpressionsMenu.Control();
                    control.name = c.name;
                    control.type = c.type;
                    control.parameter = new VRCExpressionsMenu.Control.Parameter() {name = c.parameter.name};
                    control.value = c.value;
                    control.icon = c.icon;
                    control.style = c.style;
                    control.labels = c.labels.ToArray();
                    control.subParameters = c.subParameters.Select(p => new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = p.name
                    }).ToArray();
                    control.subMenu = Serialize(c.SubmenuNode?.NodeKey);

                    return control;
                }).ToList();

                SaveAsset(menu);

                return menu;
            }
        }

        public bool ContainsMenu(VRCExpressionsMenu menu)
        {
            return _visitedMenus.Contains(menu);
        }
    }
}