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
			public VRCExpressionsMenu parent;
			public ModularAvatarMenuInstaller installer;
			public bool isInstallerRoot;
		}

		private readonly HashSet<VRCExpressionsMenu> _included;

		private readonly VRCExpressionsMenu _rootMenu;
		private readonly Dictionary<VRCExpressionsMenu, List<ChildElement>> _menuChildrenMap;

		public MenuTree(VRCAvatarDescriptor descriptor) {
			this._rootMenu = descriptor.expressionsMenu;
			this._included = new HashSet<VRCExpressionsMenu>();
			this._menuChildrenMap = new Dictionary<VRCExpressionsMenu, List<ChildElement>>();

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
			this.MappingMenu(installer);
		}

		public IEnumerable<ChildElement> GetChildren(VRCExpressionsMenu parent) {
			// TODO: ライブラリとするのであれば、複製したリスト or ImmutableArray,を返すのが好ましい
			if (parent == null) parent = this._rootMenu;
			return this._menuChildrenMap.TryGetValue(parent, out List<ChildElement> children)
				? children
				: Enumerable.Empty<ChildElement>();
		}

		public IEnumerable<ChildElement> GetChildInstallers(ModularAvatarMenuInstaller parentInstaller) {
			HashSet<VRCExpressionsMenu> visitedMenus = new HashSet<VRCExpressionsMenu>();
			Queue<VRCExpressionsMenu> queue = new Queue<VRCExpressionsMenu>();
			if (parentInstaller != null && parentInstaller.menuToAppend == null) yield break;
			if (parentInstaller == null) {
				queue.Enqueue(this._rootMenu);
			} else {
				if (parentInstaller.menuToAppend == null) yield break;
				foreach (KeyValuePair<string,VRCExpressionsMenu> childMenu in GetChildMenus(parentInstaller.menuToAppend)) {
					queue.Enqueue(childMenu.Value);
				}
			}
			while (queue.Count > 0) {
				VRCExpressionsMenu parentMenu = queue.Dequeue();
				if (visitedMenus.Contains(parentMenu)) continue;
				visitedMenus.Add(parentMenu);
				HashSet<ModularAvatarMenuInstaller> returnedInstallers = new HashSet<ModularAvatarMenuInstaller>();
				foreach (ChildElement childElement in this.GetChildren(parentMenu)) {
					if (!childElement.isInstallerRoot) {
						queue.Enqueue(childElement.menu);
						continue;
					}

					if (returnedInstallers.Contains(childElement.installer)) continue;
					returnedInstallers.Add(childElement.installer);
					yield return childElement;
				}
			}
		}


		private void MappingMenu(VRCExpressionsMenu root) {
			foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in GetChildMenus(root)) {
				this.MappingMenu(root, new ChildElement {
					menuName = childMenu.Key,
					menu = childMenu.Value
				});
			}
		}

		private void MappingMenu(ModularAvatarMenuInstaller installer) {
			IEnumerable<KeyValuePair<string, VRCExpressionsMenu>> childMenus = GetChildMenus(installer.menuToAppend);
			IEnumerable<VRCExpressionsMenu> parents = Enumerable.Empty<VRCExpressionsMenu>();
			if (installer.installTargetMenu != null &&
			    ClonedMenuMappings.TryGetClonedMenus(installer.installTargetMenu, out ImmutableArray<VRCExpressionsMenu> parentMenus)) {
				parents = parentMenus;
			}

			VRCExpressionsMenu[] parentsMenus = parents.DefaultIfEmpty(installer.installTargetMenu).ToArray();

			foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in childMenus) {
				ChildElement childElement = new ChildElement {
					menuName = childMenu.Key,
					menu = childMenu.Value,
					installer = installer,
					isInstallerRoot = true
				};
				foreach (VRCExpressionsMenu parentMenu in parentsMenus) {
					this.MappingMenu(parentMenu, childElement);
				}
			}
		}

		private void MappingMenu(VRCExpressionsMenu parent, ChildElement childElement) {
			if (parent == null) parent = this._rootMenu;
			childElement.parent = parent;
			if (!this._menuChildrenMap.TryGetValue(parent, out List<ChildElement> children)) {
				children = new List<ChildElement>();
				this._menuChildrenMap[parent] = children;
			}

			children.Add(childElement);
			if (this._included.Contains(childElement.menu)) return;
			this._included.Add(childElement.menu);
			foreach (KeyValuePair<string, VRCExpressionsMenu> childMenu in GetChildMenus(childElement.menu)) {
				this.MappingMenu(childElement.menu, new ChildElement {
					menuName = childMenu.Key,
					menu = childMenu.Value,
					installer = childElement.installer
				});
			}
		}

		private static IEnumerable<KeyValuePair<string, VRCExpressionsMenu>> GetChildMenus(VRCExpressionsMenu expressionsMenu) {
			return expressionsMenu.controls
				.Where(control => control.type == ControlType.SubMenu && control.subMenu != null)
				.Select(control => new KeyValuePair<string, VRCExpressionsMenu>(control.name, control.subMenu));
		}
	}
}