using System;
using System.Collections.Generic;
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

        public ModularAvatarMenuInstaller TargetInstaller 
        {
            get => _treeView.TargetInstaller;
            set => _treeView.TargetInstaller = value;
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

        internal static void Show(VRCAvatarDescriptor Avatar, ModularAvatarMenuInstaller Installer, Action<VRCExpressionsMenu> OnSelect)
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

        protected override TreeViewItem BuildRoot() 
        {
            _menuItems.Clear();
            _visitedMenuStack.Clear();

            _menuTree = new MenuTree(Avatar);
            _menuTree.TraverseAvatarMenu();
            foreach (ModularAvatarMenuInstaller installer in Avatar.gameObject.GetComponentsInChildren<ModularAvatarMenuInstaller>(true)) 
            {
                if (installer == TargetInstaller) continue;
                _menuTree.TraverseMenuInstaller(installer);
            }
            
            var root = new TreeViewItem(-1, -1, "<root>");
            List<TreeViewItem> treeItems = new List<TreeViewItem> 
            {
                new TreeViewItem 
                {
                    id = 0,
                    depth = 0,
                    displayName = $"{Avatar.gameObject.name} ({(Avatar.expressionsMenu == null ? "None" : Avatar.expressionsMenu.name)})"
                }
            };
            _menuItems.Add(Avatar.expressionsMenu);
            _visitedMenuStack.Push(Avatar.expressionsMenu);
            
            TraverseMenu(1, treeItems, Avatar.expressionsMenu);
            SetupParentsAndChildrenFromDepths(root, treeItems);
            return root;
        }

        private void TraverseMenu(int depth, List<TreeViewItem> items, VRCExpressionsMenu menu) 
        {
            IEnumerable<MenuTree.ChildElement> children = _menuTree.GetChildren(menu)
                .Where(child => !_visitedMenuStack.Contains(child.menu));
            foreach (MenuTree.ChildElement child in children) 
            {
                if (child.menu == null) continue;
                string displayName = child.installer == null ? 
                    $"{child.menuName} ({child.menu.name})" : 
                    $"{child.menuName} ({child.menu.name}) InstallerObject : {child.installer.name}";
                items.Add(
                    new TreeViewItem 
                    {
                        id = items.Count,
                        depth = depth,
                        displayName = displayName
                    }
                );
                _menuItems.Add(child.menu);
                _visitedMenuStack.Push(child.menu);
                TraverseMenu(depth + 1, items, child.menu);
                _visitedMenuStack.Pop();
            }
        }
    }
}