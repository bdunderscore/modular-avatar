﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.ModularAvatarSubMenuCreator;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor {
	internal class SubMenuCreateHook {
		private static readonly Texture2D _MORE_ICON = AssetDatabase.LoadAssetAtPath<Texture2D>(
			"Packages/nadena.dev.modular-avatar/Runtime/Icons/Icon_More_A.png"
		);

		private readonly Dictionary<ModularAvatarSubMenuCreator, List<ModularAvatarSubMenuCreator>> _childMap;
		private readonly List<ModularAvatarSubMenuCreator> _rootCreators;

		private readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _clonedMenus;
		private readonly Dictionary<ModularAvatarSubMenuCreator, VRCExpressionsMenu> _creatFolders;
		private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _installTargets;

		private VRCExpressionsMenu _rootMenu;

		public SubMenuCreateHook() {
			this._childMap = new Dictionary<ModularAvatarSubMenuCreator, List<ModularAvatarSubMenuCreator>>();
			this._rootCreators = new List<ModularAvatarSubMenuCreator>();
			this._clonedMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
			this._creatFolders = new Dictionary<ModularAvatarSubMenuCreator, VRCExpressionsMenu>();
		}

		public void OnPreprocessAvatar(GameObject avatarRoot) {
			this._childMap.Clear();
			this._rootCreators.Clear();
			this._clonedMenus.Clear();

			this.MappingFolderCreator(avatarRoot);
			VRCAvatarDescriptor avatar = avatarRoot.GetComponent<VRCAvatarDescriptor>();
			if (avatar.expressionsMenu == null) {
				avatar.expressionsMenu = CreateMenuAsset();
			}

			this._rootMenu = avatar.expressionsMenu;
			avatar.expressionsMenu = this.CloneMenu(avatar.expressionsMenu);
			this._installTargets = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>(this._clonedMenus);

			foreach (ModularAvatarSubMenuCreator rootCreator in this._rootCreators.Where(rootCreator => rootCreator.enabled)) {
				if (rootCreator.installTargetMenu == null) {
					rootCreator.installTargetMenu = this._rootMenu;
				}

				if (rootCreator.installTargetMenu == null) continue;
				if (!this._installTargets.TryGetValue(rootCreator.installTargetMenu, out VRCExpressionsMenu targetMenu)) continue;

				if (!this._creatFolders.TryGetValue(rootCreator, out VRCExpressionsMenu folderMenu)) {
					folderMenu = CreateMenuAsset();
					this._creatFolders[rootCreator] = folderMenu;
				}

				AddSubMenuElement(targetMenu, rootCreator.folderName, folderMenu, rootCreator.icon);
				if (!this._childMap.TryGetValue(rootCreator, out List<ModularAvatarSubMenuCreator> children)) continue;
				foreach (ModularAvatarSubMenuCreator child in children) {
					this.CreateChildFolder(child);
				}
				this.SplitMenu(rootCreator);

				this.SplitParentMenu(targetMenu, rootCreator);
			}

			ReassignmentMenuInstaller(avatarRoot);
		}


		private void CreateChildFolder(ModularAvatarSubMenuCreator creator) {
			if (!this._creatFolders.TryGetValue(creator.installTargetCreator, out VRCExpressionsMenu targetMenu)) return;
			if (!this._creatFolders.TryGetValue(creator, out VRCExpressionsMenu folderMenu)) {
				// 子が1つの親を参照する関係なので、同じ要素が複数現れることはありえない。
				// 同様に循環参照等にもたどり付けないので考慮に入れなくてよい。
				folderMenu = CreateMenuAsset();
				this._creatFolders[creator] = folderMenu;
			}

			AddSubMenuElement(targetMenu, creator.folderName, folderMenu, creator.icon);
			if (!this._childMap.TryGetValue(creator, out List<ModularAvatarSubMenuCreator> children)) return;
			foreach (ModularAvatarSubMenuCreator child in children) {
				this.CreateChildFolder(child);
			}

			this.SplitMenu(creator);
		}

		private void SplitMenu(ModularAvatarSubMenuCreator creator) {
			VRCExpressionsMenu targetMenu = this._creatFolders[creator];
			while (targetMenu.controls.Count > VRCExpressionsMenu.MAX_CONTROLS) {
				VRCExpressionsMenu newMenu = CreateMenuAsset();
				const int keepCount = VRCExpressionsMenu.MAX_CONTROLS - 1;
				newMenu.controls.AddRange(targetMenu.controls.Skip(keepCount));
				targetMenu.controls.RemoveRange(keepCount, targetMenu.controls.Count - keepCount);
				AddSubMenuElement(targetMenu, "More", newMenu, _MORE_ICON);
				this._creatFolders[creator] = newMenu;
				targetMenu = newMenu;
			}
		}

		private void SplitParentMenu(VRCExpressionsMenu targetMenu, ModularAvatarSubMenuCreator rootCreator) {
			while (targetMenu.controls.Count > VRCExpressionsMenu.MAX_CONTROLS) {
				VRCExpressionsMenu newMenu = CreateMenuAsset();
				const int keepCount = VRCExpressionsMenu.MAX_CONTROLS - 1;
				newMenu.controls.AddRange(targetMenu.controls.Skip(keepCount));
				targetMenu.controls.RemoveRange(keepCount, targetMenu.controls.Count - keepCount);
				AddSubMenuElement(targetMenu, "More", newMenu, _MORE_ICON);
				this._installTargets[rootCreator.installTargetMenu] = newMenu;
				targetMenu = newMenu;
			}
		}

		private void ReassignmentMenuInstaller(GameObject avatarRoot) {
			ModularAvatarMenuInstaller[] menuInstallers = avatarRoot.GetComponentsInChildren<ModularAvatarMenuInstaller>(true)
				.Where(installer => installer.enabled)
				.ToArray();
			foreach (ModularAvatarMenuInstaller installer in menuInstallers) {
				if (installer.installTargetMenu == null) {
					installer.installTargetMenu = this._rootMenu;
				}

				if (installer.InstallTargetType == InstallTargetType.VRCExpressionMenu || installer.installTargetCreator == null) {
					installer.installTargetMenu = this._installTargets[installer.installTargetMenu];
				} else {
					installer.installTargetMenu = this._creatFolders[installer.installTargetCreator];
				}
			}
		}

		private VRCExpressionsMenu CloneMenu(VRCExpressionsMenu menu) {
			if (menu == null) return null;
			if (this._clonedMenus.TryGetValue(menu, out VRCExpressionsMenu newMenu)) return newMenu;
			newMenu = Object.Instantiate(menu);
			AssetDatabase.CreateAsset(newMenu, Util.GenerateAssetPath());
			this._clonedMenus[menu] = newMenu;

			foreach (VRCExpressionsMenu.Control control in newMenu.controls.Where(control => control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)) {
				control.subMenu = this.CloneMenu(control.subMenu);
			}

			return newMenu;
		}

		private void MappingFolderCreator(GameObject avatarRoot) {
			foreach (ModularAvatarSubMenuCreator creator in avatarRoot.GetComponentsInChildren<ModularAvatarSubMenuCreator>(true)) {
				if (!creator.enabled) continue;
				if (creator.installTargetType == InstallTargetType.VRCExpressionMenu) {
					this._rootCreators.Add(creator);
				} else {
					if (creator.installTargetCreator == null) {
						this._rootCreators.Add(creator);
					} else {
						if (!this._childMap.TryGetValue(creator.installTargetCreator, out List<ModularAvatarSubMenuCreator> children)) {
							children = new List<ModularAvatarSubMenuCreator>();
							this._childMap[creator.installTargetCreator] = children;
						}

						children.Add(creator);
					}
				}
			}
		}

		private static void AddSubMenuElement(VRCExpressionsMenu targetMenu, string elementName, VRCExpressionsMenu subMenu, Texture2D icon = null) {
			targetMenu.controls.Add(new VRCExpressionsMenu.Control() {
				name = elementName,
				type = VRCExpressionsMenu.Control.ControlType.SubMenu,
				subMenu = subMenu,
				parameter = new VRCExpressionsMenu.Control.Parameter {
					name = ""
				},
				subParameters = Array.Empty<VRCExpressionsMenu.Control.Parameter>(),
				icon = icon,
				labels = Array.Empty<VRCExpressionsMenu.Control.Label>()
			});
		}

		private static VRCExpressionsMenu CreateMenuAsset() {
			VRCExpressionsMenu menuFolder = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
			AssetDatabase.CreateAsset(menuFolder, Util.GenerateAssetPath());
			return menuFolder;
		}
	}
}