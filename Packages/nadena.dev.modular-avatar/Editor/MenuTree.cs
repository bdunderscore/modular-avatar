using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

// ReSharper disable once CheckNamespace
namespace nadena.dev.modular_avatar.core.editor {
	public class MenuTree {
		private readonly VRCExpressionsMenu _rootMenu;
		private readonly HashSet<VRCExpressionsMenu> _included;
		private readonly Dictionary<VRCExpressionsMenu, List<VRCExpressionsMenu>> _childrenMap;

		private readonly Dictionary<VRCExpressionsMenu, ImmutableArray<VRCExpressionsMenu>> _flashedChildrenMap;

		public MenuTree(VRCAvatarDescriptor descriptor) {
			this._rootMenu = descriptor.expressionsMenu;
			this._included = new HashSet<VRCExpressionsMenu>();
			this._childrenMap = new Dictionary<VRCExpressionsMenu, List<VRCExpressionsMenu>>();

			if (this._rootMenu == null) {
				this._rootMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
			}

			this._included.Add(this._rootMenu);
		}

		public void AvatarsMenuMapping() {
			if (this._rootMenu == null) return;
			this.MappingMenu(this._rootMenu);
		}

		public void AddMenuInstaller(ModularAvatarMenuInstaller installer) {
			if (installer.menuToAppend == null) return;
			VRCExpressionsMenu parent = installer.installTargetMenu;
			if (parent == null) parent = this._rootMenu;
			this.MappingMenu(installer.menuToAppend, parent);
		}

		public IEnumerable<VRCExpressionsMenu> GetChildren(VRCExpressionsMenu parent) {
			// TODO: ライブラリとするのであれば、複製したリスト or ImmutableArray,を返すのが好ましい
			if (parent == null) parent = this._rootMenu;
			return this._childrenMap.TryGetValue(parent, out List<VRCExpressionsMenu> children) ? children : Enumerable.Empty<VRCExpressionsMenu>();
		}

		private void MappingMenu(VRCExpressionsMenu root, VRCExpressionsMenu customParent = null) {
			Queue<VRCExpressionsMenu> queue = new Queue<VRCExpressionsMenu>();
			queue.Enqueue(root);

			while (queue.Count > 0) {
				VRCExpressionsMenu parent = queue.Dequeue();
				IEnumerable<VRCExpressionsMenu> subMenus = GetSubMenus(parent);
				if (customParent != null) {
					parent = customParent;
					customParent = null;
				}
				foreach (VRCExpressionsMenu subMenu in subMenus) {
					if (!this._childrenMap.TryGetValue(parent, out List<VRCExpressionsMenu> children)) {
						children = new List<VRCExpressionsMenu>();
						this._childrenMap[parent] = children;
					}

					children.Add(subMenu);
					if (this._included.Contains(subMenu)) continue;
					queue.Enqueue(subMenu);
					this._included.Add(subMenu);
				}
			}
		}

		private static IEnumerable<VRCExpressionsMenu> GetSubMenus(VRCExpressionsMenu expressionsMenu) {
			return expressionsMenu.controls
				.Where(control => control.type == ControlType.SubMenu && control.subMenu != null)
				.Select(control => control.subMenu);
		}
	}
}