#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.core.menu;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
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
        internal delegate VirtualMenuNode
            NodeForDelegate(object menu, Action<VRCExpressionsMenu.Control> postprocessor);

        private readonly ImmutableDictionary<object, ImmutableList<ModularAvatarMenuInstaller>>
            _menuToInstallerMap;

        private readonly ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>
            _postProcessControls
                = ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>>.Empty;

        private readonly VirtualMenuNode _node;
        private readonly NodeForDelegate _nodeFor;
        private readonly Action<VRCExpressionsMenu> _visitedMenu;
        private readonly HashSet<object> _visited = new HashSet<object>();

        private Action<VRCExpressionsMenu.Control> _currentPostprocessor;

        internal ImmutableHashSet<object> Visited => _visited.ToImmutableHashSet();

        private class PostprocessorContext : IDisposable
        {
            private NodeContextImpl _context;
            private Action<VRCExpressionsMenu.Control> _priorPostprocessor;

            public PostprocessorContext(NodeContextImpl context, Action<VRCExpressionsMenu.Control> postprocessor)
            {
                this._context = context;
                this._priorPostprocessor = context._currentPostprocessor;
                context._currentPostprocessor = postprocessor ?? context._currentPostprocessor;
            }

            public void Dispose()
            {
                _context._currentPostprocessor = _priorPostprocessor;
            }
        }

        public NodeContextImpl(
            VirtualMenuNode node,
            NodeForDelegate nodeFor,
            ImmutableDictionary<object, ImmutableList<ModularAvatarMenuInstaller>> menuToInstallerMap,
            ImmutableDictionary<ModularAvatarMenuInstaller, Action<VRCExpressionsMenu.Control>> postProcessControls,
            Action<VRCExpressionsMenu> visitedMenu,
            Action<VRCExpressionsMenu.Control> postprocessor
        )
        {
            _node = node;
            _nodeFor = nodeFor;
            _menuToInstallerMap = menuToInstallerMap;
            _postProcessControls = postProcessControls;
            _visitedMenu = visitedMenu;
            _currentPostprocessor = postprocessor;
        }

        public void PushMenuContents(VRCExpressionsMenu expMenu)
        {
            if (expMenu == null) return;
            if (_visited.Contains(expMenu)) return;
            _visited.Add(expMenu);
            _visitedMenu(expMenu);

            try
            {
                foreach (var control in expMenu.controls)
                {
                    PushControl(control);
                }

                if (_menuToInstallerMap.TryGetValue(ObjectRegistry.GetReference(expMenu), out var installers))
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
            finally
            {
                // We can visit the same expMenu multiple times, with different visit contexts (owing to having
                // different source installers, with different postprocessing configurations).
                _visited.Remove(expMenu);
            }
        }

        public void PushNode(MenuSource source)
        {
            if (source == null) return;
            if (_visited.Contains(source)) return;
            _visited.Add(source);

            BuildReport.ReportingObject(source as UnityEngine.Object, () => source.Visit(this));

            _visited.Remove(source);
        }

        public void PushNode(ModularAvatarMenuInstaller installer)
        {
            if (installer == null) return;
            if (_visited.Contains(installer)) return;
            _visited.Add(installer);

            BuildReport.ReportingObject(installer, () =>
            {
                using (new PostprocessorContext(this, _postProcessControls.GetValueOrDefault(installer)))
                {
                    var menuSourceComp = installer.GetComponent<MenuSource>();
                    if (menuSourceComp != null)
                    {
                        PushNode(menuSourceComp);
                    }
                    else if (installer.menuToAppend != null)
                    {
                        PushMenuContents(installer.menuToAppend);
                    }
                }
            });

            _visited.Remove(installer);
        }

        public void PushControl(VRCExpressionsMenu.Control control)
        {
            // XXX: When we invoke NodeFor on the subMenu, we need to ensure we dedup considering the parameter context
            // of the source control. This is because the same subMenu can be used in multiple places, with different
            // parameter replacements. (FIXME)
            var virtualControl = new VirtualControl(control);

            if (control.subMenu != null)
            {
                virtualControl.SubmenuNode = NodeFor(control.subMenu);
            }

            _currentPostprocessor(virtualControl);

            PushControl(virtualControl);
        }

        public void PushControl(VirtualControl control)
        {
            _node.Controls.Add(control);
        }

        public VirtualMenuNode NodeFor(VRCExpressionsMenu menu)
        {
            if (menu == null) return null;
            return _nodeFor(menu, _currentPostprocessor);
        }

        public VirtualMenuNode NodeFor(MenuSource source)
        {
            if (source == null) return null;
            return _nodeFor(source, _currentPostprocessor);
        }
    }

    /**
     * The VirtualMenu class tracks a fully realized shadow menu. Notably, this is _not_ converted to unity
     * ScriptableObjects, making it easier to discard it when we need to update it.
     */
    internal class VirtualMenu
    {
        private static readonly Action<VRCExpressionsMenu.Control> NoopPostprocessor = control => { };

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
        private ImmutableHashSet<object> _visitedNodes = ImmutableHashSet<object>.Empty;

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
                RootMenuKey = (ValueTuple<object, object>) (rootMenu, NoopPostprocessor);
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

            var target = installer.installTargetMenu ? (object) ObjectRegistry.GetReference(installer.installTargetMenu) : RootMenuKey;
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
                    m => _visitedMenus.Add(m),
                    NoopPostprocessor);
            if (RootMenuKey is ValueTuple<object, object> tuple && tuple.Item1 is VRCExpressionsMenu menu)
            {
                foreach (var control in menu.controls)
                {
                    rootContext.PushControl(control);
                }

                // Some menu installers may be bound to the root menu _asset_ directly.
                if (menuToInstallerFiltered.TryGetValue(ObjectRegistry.GetReference(menu), out var installers))
                {
                    foreach (var installer in installers)
                    {
                        rootContext.PushNode(installer);
                    }
                }
            }

            // Untargeted installers are bound to the RootMenuKey, rather than the menu asset itself.
            if (menuToInstallerFiltered.TryGetValue(RootMenuKey, out var installers2))
            {
                foreach (var installer in installers2)
                {
                    rootContext.PushNode(installer);
                }
            }

            while (_pendingGeneration.Count > 0)
            {
                _pendingGeneration.Dequeue()();
            }

            _visitedNodes = rootContext.Visited;

            VirtualMenuNode NodeFor(object key, Action<VRCExpressionsMenu.Control> postprocessContext)
            {
                var lookupKey = key;
                if (key is VRCExpressionsMenu)
                {
                    lookupKey = (ValueTuple<object, object>) (key, postprocessContext);
                }

                if (_resolvedMenu.TryGetValue(lookupKey, out var node)) return node;
                node = new VirtualMenuNode(lookupKey);
                _resolvedMenu[lookupKey] = node;

                _pendingGeneration.Enqueue(() =>
                {
                    BuildReport.ReportingObject(key as UnityEngine.Object, () =>
                    {
                        var context = new NodeContextImpl(node, NodeFor, menuToInstallerFiltered,
                            _postprocessControlsHooks,
                            m => _visitedMenus.Add(m),
                            postprocessContext);
                        if (key is VRCExpressionsMenu expMenu)
                        {
                            context.PushMenuContents(expMenu);
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

        public bool ContainsNode(ModularAvatarMenuItem item)
        {
            return _visitedNodes.Contains(item);
        }
    }
}

#endif