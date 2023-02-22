using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.menu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace nadena.dev.modular_avatar.core.editor
{
    class AvMenuTreeViewWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avatarDescriptor;
        private AvMenuTreeView _treeView;

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
        }

        private void OnLostFocus()
        {
            //Close();
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

            _treeView.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        internal static void Show(VRCAvatarDescriptor Avatar, ModularAvatarMenuInstaller Installer,
            Action<object> OnSelect)
        {
            var window = GetWindow<AvMenuTreeViewWindow>();
            window.titleContent = new GUIContent("Select menu");

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

            var root = new TreeViewItem(-1, -1, "<root>");
            List<TreeViewItem> treeItems = new List<TreeViewItem>
            {
                new TreeViewItem
                {
                    id = 0,
                    depth = 0,
                    displayName =
                        $"{Avatar.gameObject.name} ({(Avatar.expressionsMenu == null ? "None" : Avatar.expressionsMenu.name)})"
                }
            };
            _nodeKeys.Add(_menuTree.RootMenuKey);
            _visitedMenuStack.Push(_menuTree.RootMenuKey);
            TraverseMenu(1, treeItems, _menuTree.RootMenuNode);
            SetupParentsAndChildrenFromDepths(root, treeItems);
            return root;
        }

        private void TraverseMenu(int depth, List<TreeViewItem> items, MenuNode node)
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