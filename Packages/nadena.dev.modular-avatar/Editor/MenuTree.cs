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
		public struct ChildElement {
			public string menuName;
			public VRCExpressionsMenu menu;
			public ModularAvatarMenuInstaller installer;
		}
		
		private readonly VRCExpressionsMenu _rootMenu;
		private readonly HashSet<VRCExpressionsMenu> _included;
		private readonly Dictionary<VRCExpressionsMenu, List<ChildElement>> _childrenMap;

		private readonly Dictionary<VRCExpressionsMenu, ImmutableArray<VRCExpressionsMenu>> _flashedChildrenMap;

		public MenuTree(VRCAvatarDescriptor descriptor) {
			this._rootMenu = descriptor.expressionsMenu;
			this._included = new HashSet<VRCExpressionsMenu>();
			this._childrenMap = new Dictionary<VRCExpressionsMenu, List<ChildElement>>();

			if (this._rootMenu == null) {
				this._rootMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
			}

			this._included.Add(this._rootMenu);
		}

		public void AvatarsMenuMapping() {
			if (this._rootMenu == null) return;
			this.MappingMenu(this._rootMenu);
		}

		public void MappingMenuInstaller(ModularAvatarMenuInstaller installer) {
			if (!installer.enabled) return;
			if (installer.menuToAppend == null) return;
			this.MappingMenu(installer.menuToAppend, installer);
		}

		public IEnumerable<ChildElement> GetChildren(VRCExpressionsMenu parent) {
			// TODO: ライブラリとするのであれば、複製したリスト or ImmutableArray,を返すのが好ましい
			if (parent == null) parent = this._rootMenu;
			return this._childrenMap.TryGetValue(parent, out List<ChildElement> children)
				? children
				: Enumerable.Empty<ChildElement>();
		}

		private void MappingMenu(VRCExpressionsMenu root, ModularAvatarMenuInstaller installer = null) {
			Queue<VRCExpressionsMenu> queue = new Queue<VRCExpressionsMenu>();
			queue.Enqueue(root);
			bool first = true;

			while (queue.Count > 0) {
				VRCExpressionsMenu parent = queue.Dequeue();
				IEnumerable<KeyValuePair<string, VRCExpressionsMenu>> childMenus = GetChildMenus(parent);
				
				if (first && installer != null) {
					parent = installer.installTargetMenu != null ? installer.installTargetMenu : _rootMenu;
					
				}

				foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in childMenus) {
					if (!this._childrenMap.TryGetValue(parent, out List<ChildElement> children)) {
						children = new List<ChildElement>();
						this._childrenMap[parent] = children;
					}

					ChildElement childElement = new ChildElement { menuName = childMenu.Key, menu = childMenu.Value, installer = installer };
					children.Add(childElement);
					if (this._included.Contains(childElement.menu)) continue;
					queue.Enqueue(childElement.menu);
					this._included.Add(childElement.menu);
				}

				first = false;
			}
		}

		private static IEnumerable<KeyValuePair<string, VRCExpressionsMenu>> GetChildMenus(VRCExpressionsMenu expressionsMenu) {
			return expressionsMenu.controls
				.Where(control => control.type == ControlType.SubMenu && control.subMenu != null)
				.Select(control => new KeyValuePair<string, VRCExpressionsMenu>(control.name, control.subMenu));
		}
	}
}