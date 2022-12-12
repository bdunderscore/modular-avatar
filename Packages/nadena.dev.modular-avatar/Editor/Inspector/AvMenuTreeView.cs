using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

        public Action<VRCExpressionsMenu> OnMenuSelected = (menu) => { };

        private void Awake()
        {
            _treeView = new AvMenuTreeView(new TreeViewState());
            _treeView.OnSelect = (menu) => OnMenuSelected.Invoke(menu);
            _treeView.OnDoubleclickSelect = Close;
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

            _treeView.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        internal static void Show(VRCAvatarDescriptor Avatar, Action<VRCExpressionsMenu> OnSelect)
        {
            var window = GetWindow<AvMenuTreeViewWindow>();
            window.titleContent = new GUIContent("Select menu");

            window.Avatar = Avatar;
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

        internal Action<VRCExpressionsMenu> OnSelect = (menu) => { };
        internal Action OnDoubleclickSelect = () => { };

        private List<VRCExpressionsMenu> _menuItems = new List<VRCExpressionsMenu>();
        private HashSet<VRCExpressionsMenu> _visitedMenus = new HashSet<VRCExpressionsMenu>();

        private MenuTree _menuTree;
        private Stack<VRCExpressionsMenu> _visitedMenuStack = new Stack<VRCExpressionsMenu>();

        public AvMenuTreeView(TreeViewState state) : base(state)
        {
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            OnSelect.Invoke(_menuItems[selectedIds[0]]);
        }

        protected override void DoubleClickedItem(int id)
        {
            OnSelect.Invoke(_menuItems[id]);
            OnDoubleclickSelect.Invoke();
        }

        protected override TreeViewItem BuildRoot() {
            this._menuItems.Clear();
            this._visitedMenuStack.Clear();
            
            if (Avatar.expressionsMenu == null) {
                // TODO: nullの場合もツリーを表示できるように(Installerがあれば子もあるので)
                return new TreeViewItem(0, -1, "No menu");
            }

            _menuTree = new MenuTree(Avatar);
            _menuTree.AvatarsMenuMapping();
            
            var root = new TreeViewItem(-1, -1, "<root>");
            List<TreeViewItem> treeItems = new List<TreeViewItem> {
                new TreeViewItem {
                    id = 0,
                    depth = 0,
                    displayName = $"{Avatar.gameObject.name} ({Avatar.expressionsMenu.name})"
                }
            };
            this._menuItems.Add(Avatar.expressionsMenu);
            this._visitedMenuStack.Push(Avatar.expressionsMenu);
            
            this.TraverseMenu(1, treeItems, Avatar.expressionsMenu);
            SetupParentsAndChildrenFromDepths(root, treeItems);
            return root;
        }

        private void TraverseMenu(int depth, List<TreeViewItem> items, VRCExpressionsMenu menu) {
            IEnumerable<VRCExpressionsMenu> children = this._menuTree.GetChildren(menu)
                .Where(child => !this._visitedMenuStack.Contains(child));
            foreach (VRCExpressionsMenu child in children) {
                items.Add(
                    new TreeViewItem {
                        id = items.Count,
                        depth = depth,
                        displayName = $"{ /*control.name*/null} ({child.name})" // TODO: サブメニュー名を取ってこれるように
                    }
                );
                this._menuItems.Add(child);
                this._visitedMenuStack.Push(child);
                this.TraverseMenu(depth + 1, items, child);
                this._visitedMenuStack.Pop();
            }
        }

        /*
        protected override TreeViewItem BuildRoot()
        {
            _menuItems.Clear();
            _visitedMenus.Clear();

            if (Avatar.expressionsMenu == null)
            {
                return new TreeViewItem(0, -1, "No menu");
            }

            _visitedMenus.Add(Avatar.expressionsMenu);
            _menuItems.Add(Avatar.expressionsMenu);
            var root = new TreeViewItem {id = -1, depth = -1, displayName = "<root>"};

            var treeItems = new List<TreeViewItem>();
            treeItems.Add(new TreeViewItem
                {id = 0, depth = 0, displayName = $"{Avatar.gameObject.name} ({Avatar.expressionsMenu.name})"});

            TraverseMenu(1, treeItems, Avatar.expressionsMenu);

            SetupParentsAndChildrenFromDepths(root, treeItems);

            return root;
        }

        private void TraverseMenu(int depth, List<TreeViewItem> items, VRCExpressionsMenu menu)
        {
            foreach (var control in menu.controls)
            {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && control.subMenu != null && !_visitedMenus.Contains(control.subMenu))
                {
                    items.Add(new TreeViewItem
                    {
                        id = _menuItems.Count,
                        depth = depth,
                        displayName = $"{control.name} ({control.subMenu.name})"
                    });
                    _menuItems.Add(control.subMenu);
                    _visitedMenus.Add(control.subMenu);

                    TraverseMenu(depth + 1, items, control.subMenu);
                }
            }
        }
        */
    }
}