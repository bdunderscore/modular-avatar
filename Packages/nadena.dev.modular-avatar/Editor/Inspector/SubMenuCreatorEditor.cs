using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.ModularAvatarSubMenuCreator;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor {
	[CustomEditor(typeof(ModularAvatarSubMenuCreator))]
	[CanEditMultipleObjects]
	internal class SubMenuCreatorEditor : MAEditorBase {
		private ModularAvatarSubMenuCreator _creator;
		private HashSet<VRCExpressionsMenu> _avatarMenus;
		private HashSet<ModularAvatarSubMenuCreator> _menuFolderCreators;

		private void OnEnable() {
			this._creator = (ModularAvatarSubMenuCreator)this.target;
			this.FindMenus();
			this.FindMenuFolderCreators();
		}

		protected override void OnInnerInspectorGUI() {
			VRCAvatarDescriptor commonAvatar = this.FindCommonAvatar();

			SerializedProperty installTargetTypeProperty = this.serializedObject.FindProperty(nameof(ModularAvatarSubMenuCreator.installTargetType));
			EditorGUILayout.PropertyField(installTargetTypeProperty, new GUIContent("Install Target Type"));
			InstallTargetType installTargetType = (InstallTargetType)Enum.ToObject(typeof(InstallTargetType), installTargetTypeProperty.enumValueIndex);

			if (!installTargetTypeProperty.hasMultipleDifferentValues) {
				string installTargetMenuPropertyName;
				Type installTargetObjectType;
				if (installTargetType == InstallTargetType.VRCExpressionMenu) {
					installTargetMenuPropertyName = nameof(ModularAvatarSubMenuCreator.installTargetMenu);
					installTargetObjectType = typeof(VRCExpressionsMenu);
				} else {
					installTargetMenuPropertyName = nameof(ModularAvatarSubMenuCreator.installTargetCreator);
					installTargetObjectType = typeof(ModularAvatarSubMenuCreator);
					commonAvatar = null;
				}

				SerializedProperty installTargetProperty = this.serializedObject.FindProperty(installTargetMenuPropertyName);
				this.ShowMenuFolderCreateHelpBox(installTargetProperty, installTargetType);
				this.ShowInstallTargetPropertyField(installTargetProperty, commonAvatar, installTargetObjectType);

				VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(this._creator.transform);
				if (avatar != null && GUILayout.Button(Localization.G("menuinstall.selectmenu"))) {
					if (installTargetType == InstallTargetType.VRCExpressionMenu) {
						AvMenuTreeViewWindow.Show(avatar, menu => {
							installTargetProperty.objectReferenceValue = menu;
							serializedObject.ApplyModifiedProperties();
						});
					} else {
						AvMenuFolderCreatorTreeViewWindow.Show(avatar, this._creator, creator => {
							installTargetProperty.objectReferenceValue = creator;
							serializedObject.ApplyModifiedProperties();
						});
					}
				}
			}

			SerializedProperty folderNameProperty = this.serializedObject.FindProperty(nameof(ModularAvatarSubMenuCreator.folderName));
			EditorGUILayout.PropertyField(folderNameProperty, new GUIContent("Folder Name"));

			SerializedProperty iconProperty = this.serializedObject.FindProperty(nameof(ModularAvatarSubMenuCreator.icon));
			EditorGUILayout.PropertyField(iconProperty, new GUIContent("Folder Icon"));

			serializedObject.ApplyModifiedProperties();
			Localization.ShowLanguageUI();
		}

		private void ShowMenuFolderCreateHelpBox(SerializedProperty installTargetProperty, InstallTargetType installTargetType) {
			if (installTargetProperty.hasMultipleDifferentValues) return;
			bool isEnabled = this.targets.Length != 1 || this._creator.enabled;

			if (installTargetProperty.objectReferenceValue == null) {
				if (!isEnabled) return;
				EditorGUILayout.HelpBox(Localization.S("menuinstall.help.hint_set_menu"), MessageType.Info);
			} else {
				VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(this._creator.transform);
				switch (installTargetType) {
					case InstallTargetType.VRCExpressionMenu:
						if (!this.IsMenuReachable(avatar, (VRCExpressionsMenu)installTargetProperty.objectReferenceValue)) {
							EditorGUILayout.HelpBox(Localization.S("menuinstall.help.hint_bad_menu"), MessageType.Error);
						}

						break;
					case InstallTargetType.FolderCreator:
						if (!this.IsMenuReachable(avatar, (ModularAvatarSubMenuCreator)installTargetProperty.objectReferenceValue, 
							    new HashSet<ModularAvatarSubMenuCreator>())) {
							EditorGUILayout.HelpBox("選択されたメニューフォルダからアバターまでのパスが見つかりません。", MessageType.Error);
						}

						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(installTargetType), installTargetType, null);
				}
			}
		}

		private void ShowInstallTargetPropertyField(SerializedProperty installTargetProperty, VRCAvatarDescriptor avatar, Type propertyType) {
			Object displayValue = installTargetProperty.objectReferenceValue;
			if (!installTargetProperty.hasMultipleDifferentValues && avatar != null) {
				if (displayValue == null) displayValue = avatar.expressionsMenu;
			}

			EditorGUI.BeginChangeCheck();
			Object newValue = EditorGUILayout.ObjectField(Localization.G("menuinstall.installto"), displayValue, propertyType,
				propertyType == typeof(ModularAvatarSubMenuCreator));
			if (newValue == this._creator) newValue = displayValue;
			if (EditorGUI.EndChangeCheck()) {
				installTargetProperty.objectReferenceValue = newValue;
			}
		}

		private VRCAvatarDescriptor FindCommonAvatar() {
			VRCAvatarDescriptor commonAvatar = null;
			foreach (Object targetObject in targets) {
				ModularAvatarSubMenuCreator component = (ModularAvatarSubMenuCreator)targetObject;
				VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(component.transform);
				if (avatar == null) return null;

				if (commonAvatar == null) {
					commonAvatar = avatar;
				} else if (commonAvatar != avatar) {
					return null;
				}
			}

			return commonAvatar;
		}

		private void FindMenus() {
			if (this.targets.Length > 1) {
				this._avatarMenus = null;
				return;
			}

			this._avatarMenus = new HashSet<VRCExpressionsMenu>();
			Queue<VRCExpressionsMenu> queue = new Queue<VRCExpressionsMenu>();
			VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(this._creator.transform);
			if (avatar == null || avatar.expressionsMenu == null) return;
			queue.Enqueue(avatar.expressionsMenu);

			while (queue.Count > 0) {
				var menu = queue.Dequeue();
				if (this._avatarMenus.Contains(menu)) continue;

				this._avatarMenus.Add(menu);
				IEnumerable<VRCExpressionsMenu> subMenus = menu.controls
					.Where(control => control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
					.Select(control => control.subMenu);

				foreach (VRCExpressionsMenu subMenu in subMenus) {
					queue.Enqueue(subMenu);
				}
			}
		}

		private void FindMenuFolderCreators() {
			if (this.targets.Length > 1) {
				this._menuFolderCreators = null;
				return;
			}

			this._menuFolderCreators = new HashSet<ModularAvatarSubMenuCreator>();
			VRCAvatarDescriptor avatar = RuntimeUtil.FindAvatarInParents(this._creator.transform);
			if (avatar == null) return;
			foreach (ModularAvatarSubMenuCreator creator in avatar.gameObject
				         .GetComponentsInChildren<ModularAvatarSubMenuCreator>()
				         .Where(creator => creator != this._creator)) {
				this._menuFolderCreators.Add(creator);
			}
		}

		private bool IsMenuReachable(VRCAvatarDescriptor avatar, VRCExpressionsMenu menu) {
			return this._avatarMenus == null || this._avatarMenus.Contains(menu);
		}

		private bool IsMenuReachable(VRCAvatarDescriptor avatar, ModularAvatarSubMenuCreator creator, HashSet<ModularAvatarSubMenuCreator> session) {
			if (avatar == null) return true;
			if (this._menuFolderCreators == null) return true;

			if (session.Contains(creator)) return false;
			if (!this._menuFolderCreators.Contains(creator)) return false;

			if (!creator.enabled) return false;
			session.Add(creator);
			switch (creator.installTargetType) {
				case InstallTargetType.VRCExpressionMenu:
					return creator.installTargetMenu == null || this.IsMenuReachable(avatar, creator.installTargetMenu);
				case InstallTargetType.FolderCreator:
					return creator.installTargetCreator == null || this.IsMenuReachable(avatar, creator.installTargetCreator, session);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}