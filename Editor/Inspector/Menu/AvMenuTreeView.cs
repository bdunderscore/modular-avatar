#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.menu;
using nadena.dev.modular_avatar.core.menu;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;


namespace nadena.dev.modular_avatar.core.editor
{
    class AvMenuTreeViewWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avatarDescriptor;
        private AvMenuTreeView _treeView;
        private long _cacheIndex = -1;

        public VRCAvatarDescriptor Avatar
        {
            get => _treeView.Avatar;
            set => _treeView.Avatar = value;
        }

        public ModularAvatarMenuInstaller TargetInstaller
        {
            get => _treeView.TargetInstaller;
            set => _treeView.TargetInstaller = value;
        }

        public Action<object> OnMenuSelected = (menu) => { };

        private void Awake()
        {
            _treeView = new AvMenuTreeView(new TreeViewState());
            _treeView.OnSelect = (menu) => OnMenuSelected.Invoke(menu);
            _treeView.OnDoubleclickSelect = Close;
            _cacheIndex = -1;
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void OnDisable()
        {
            OnMenuSelected = (menu) => { };
        }

        private void OnGUI()
        {
            if (_treeView == null || _treeView.Avatar == null)
            {
                Close();
                return;
            }

            if (_cacheIndex != VirtualMenu.CacheSequence)
            {
                _treeView.ReloadPreservingExpanded();
                _cacheIndex = VirtualMenu.CacheSequence;
            }

            _treeView.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        internal static void Show(VRCAvatarDescriptor Avatar, ModularAvatarMenuInstaller Installer,
            Action<object> OnSelect)
        {
            var window = GetWindow<AvMenuTreeViewWindow>();
            window.titleContent = G("menu_tree.title");

            window.Avatar = Avatar;
            window.TargetInstaller = Installer;
            window.OnMenuSelected = OnSelect;

            window.Show();
        }
    }

    class AvMenuTreeView : TreeView
    {
        private VRCAvatarDescriptor _avatar;

        public VRCAvatarDescriptor Avatar
        {
            get => _avatar;
            set
            {
                _avatar = value;
                Reload();
            }
        }

        private ModularAvatarMenuInstaller _targetInstaller;

        public ModularAvatarMenuInstaller TargetInstaller
        {
            get => _targetInstaller;
            set
            {
                _targetInstaller = value;
                Reload();
            }
        }

        internal Action<object> OnSelect = (menu) => { };
        internal Action OnDoubleclickSelect = () => { };

        private List<object> _nodeKeys = new List<object>();
        private HashSet<object> _visitedMenus = new HashSet<object>();

        private VirtualMenu _menuTree;
        private Stack<object> _visitedMenuStack = new Stack<object>();

        public AvMenuTreeView(TreeViewState state) : base(state)
        {
        }

        public void ReloadPreservingExpanded()
        {
            var expanded = GetExpanded().Select(id => _nodeKeys[id]).ToImmutableHashSet();
            var selected = GetSelection().Select(id => _nodeKeys[id]).ToImmutableHashSet();
            CollapseAll();
            Reload();
            SetExpanded(Enumerable.Range(0, _nodeKeys.Count)
                .Where(i => _nodeKeys[i] != null && expanded.Contains(_nodeKeys[i]))
                .ToList());
            SetSelection(Enumerable.Range(0, _nodeKeys.Count)
                .Where(i => _nodeKeys[i] != null && selected.Contains(_nodeKeys[i]))
                .ToList());
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            OnSelect.Invoke(_nodeKeys[selectedIds[0]]);
        }

        protected override void DoubleClickedItem(int id)
        {
            OnSelect.Invoke(_nodeKeys[id]);
            OnDoubleclickSelect.Invoke();
        }

        protected override TreeViewItem BuildRoot()
        {
            _nodeKeys.Clear();
            _visitedMenuStack.Clear();

            _menuTree = VirtualMenu.ForAvatar(_avatar);

            var preferredRoot = FindPreferredRoot(_menuTree);
            var rootName = "";
            if (preferredRoot is ModularAvatarMenuGroup group)
            {
                if (group.targetObject != null) rootName = $"({group.targetObject.name})";
                else rootName = $"({group.gameObject.name})";
            }
            else if (preferredRoot is VRCExpressionsMenu menu)
            {
                rootName = $"({menu.name})";
            }

            var root = new TreeViewItem(-1, -1, "<root>");
            List<TreeViewItem> treeItems = new List<TreeViewItem>
            {
                new TreeViewItem
                {
                    id = 0,
                    depth = 0,
                    displayName =
                        $"{Avatar.gameObject.name} {rootName}"
                }
            };

            _nodeKeys.Add(preferredRoot);
            _visitedMenuStack.Push(preferredRoot);
            TraverseMenu(1, treeItems, _menuTree.RootMenuNode);
            SetupParentsAndChildrenFromDepths(root, treeItems);
            return root;
        }

        private object FindPreferredRoot(VirtualMenu menuTree)
        {
            // There's always a VRCExpressionsMenu at the root, but we'd prefer to add stuff under a MenuItem tree if
            // available. See if we can find one.
            foreach (var installer in _avatar.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                if (installer.installTargetMenu != null &&
                    !ReferenceEquals(installer.installTargetMenu, menuTree.RootMenuKey))
                {
                    continue;
                }

                var menuSource = installer.GetComponent<MenuSource>();
                if (menuSource == null || !(menuSource is ModularAvatarMenuGroup group)) continue;

                return menuSource;
            }

            return menuTree.RootMenuKey;
        }

        private void TraverseMenu(int depth, List<TreeViewItem> items, VirtualMenuNode node)
        {
            IEnumerable<VirtualControl> children = node.Controls
                .Where(control => control.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
                                  control.SubmenuNode != null &&
                                  !_visitedMenuStack.Contains(control.SubmenuNode));
            foreach (var child in children)
            {
                string displayName = child.name;

                items.Add(
                    new TreeViewItem
                    {
                        id = items.Count,
                        depth = depth,
                        displayName = displayName
                    }
                );
                _nodeKeys.Add(child.SubmenuNode.NodeKey);
                _visitedMenuStack.Push(child.SubmenuNode);
                TraverseMenu(depth + 1, items, child.SubmenuNode);
                _visitedMenuStack.Pop();
            }
        }
    }
}

#endif