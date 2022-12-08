using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor {
	public class AvMenuFolderCreatorTreeViewWindow : EditorWindow {
		private AvMenuFolderCreatorTreeView _treeView;

		private VRCAvatarDescriptor Avatar {
			set => this._treeView.Avatar = value;
		}

		private ModularAvatarSubMenuCreator Creator {
			set => this._treeView.Creator = value;
		}

		private Action<ModularAvatarSubMenuCreator> OnMenuSelected = (creator) => { };

		private void Awake() {
			this._treeView = new AvMenuFolderCreatorTreeView(new TreeViewState()) {
				OnSelect = (creator) => this.OnMenuSelected.Invoke(creator),
				onDoubleClickSelect = this.Close
			};
		}

		private void OnLostFocus() {
			this.Close();
		}

		private void OnDisable() {
			this.OnMenuSelected = (creator) => { };
		}

		private void OnGUI() {
			if (this._treeView == null || this._treeView.Avatar == null) {
				this.Close();
				return;
			}

			this._treeView.OnGUI(new Rect(0, 0, this.position.width, this.position.height));
		}

		internal static void Show(VRCAvatarDescriptor avatar, ModularAvatarSubMenuCreator creator, Action<ModularAvatarSubMenuCreator> OnSelect) {
			AvMenuFolderCreatorTreeViewWindow window = GetWindow<AvMenuFolderCreatorTreeViewWindow>();
			window.titleContent = new GUIContent("Select menu folder creator");

			window.Avatar = avatar;
			window.Creator = creator;
			window.OnMenuSelected = OnSelect;

			window.Show();
		}
	}

	public class AvMenuFolderCreatorTreeView : TreeView {
		private VRCAvatarDescriptor _avatar;
		public VRCAvatarDescriptor Avatar {
			get => this._avatar;
			set {
				this._avatar = value;
				this.Reload();
			}
		}

		private ModularAvatarSubMenuCreator _creator;
		public ModularAvatarSubMenuCreator Creator {
			get => this._creator;
			set {
				this._creator = value;
				this.Reload();
			}
		}

		private int _currentCreatorIndex;
		private readonly Texture2D _currentBackgroundTexture;

		internal Action<ModularAvatarSubMenuCreator> OnSelect = (creator) => { };
		internal Action onDoubleClickSelect = () => { };

		private readonly List<ModularAvatarSubMenuCreator> _creatorItems = new List<ModularAvatarSubMenuCreator>();
		private readonly HashSet<ModularAvatarSubMenuCreator> _visitedCreators = new HashSet<ModularAvatarSubMenuCreator>();

		private Dictionary<ModularAvatarSubMenuCreator, List<ModularAvatarSubMenuCreator>> _childMap;
		private List<ModularAvatarSubMenuCreator> _rootCreators;

		public AvMenuFolderCreatorTreeView(TreeViewState state) : base(state) {
			this._currentBackgroundTexture = new Texture2D(1, 1);
			this._currentBackgroundTexture.SetPixel(0, 0, new Color(0.0f, 0.3f, 0.0f));
			this._currentBackgroundTexture.Apply();
		}

		protected override void SelectionChanged(IList<int> selectedIds) {
			if (selectedIds[0] == this._currentCreatorIndex) return;
			this.OnSelect.Invoke(this._creatorItems[selectedIds[0]]);
			this.Reload();
		}

		protected override void DoubleClickedItem(int id) {
			if (id == this._currentCreatorIndex) return;
			this.OnSelect.Invoke(this._creatorItems[id]);
			this.onDoubleClickSelect.Invoke();
		}

		protected override TreeViewItem BuildRoot() {
			this._creatorItems.Clear();
			this._visitedCreators.Clear();
			this._currentCreatorIndex = -1;
			this.MappingFolderCreator();

			TreeViewItem root = new TreeViewItem(-1, -1, "<root>");
			List<TreeViewItem> treeItems = new List<TreeViewItem>();
			treeItems.Add(new TreeViewItem {
				id = treeItems.Count,
				depth = 0,
				displayName = $"{this._avatar.name} ({(this._avatar.expressionsMenu != null ? this._avatar.expressionsMenu.name : null)})"
			});
			this._creatorItems.Add(null);

			foreach (ModularAvatarSubMenuCreator rootCreator in this._rootCreators) {
				bool isCurrent = rootCreator == this.Creator;
				if (isCurrent) {
					this._currentCreatorIndex = treeItems.Count;
				}
				treeItems.Add(new TreeViewItem {
					id = treeItems.Count,
					depth = 1,
					displayName = isCurrent ? "This" : $"{rootCreator.name} ({rootCreator.folderName})"
				});
				this._creatorItems.Add(rootCreator);
				this._visitedCreators.Add(rootCreator);
				if (isCurrent) continue;
				this.TraverseCreator(2, treeItems, rootCreator);
			}

			SetupParentsAndChildrenFromDepths(root, treeItems);
			return root;
		}

		private void TraverseCreator(int depth, List<TreeViewItem> items, ModularAvatarSubMenuCreator creator) {
			if (!this._childMap.TryGetValue(creator, out List<ModularAvatarSubMenuCreator> children)) return;
			foreach (ModularAvatarSubMenuCreator child in children.Where(child => !this._visitedCreators.Contains(child))) {
				bool isCurrent = child == this.Creator;
				if (isCurrent) {
					this._currentCreatorIndex = items.Count;
				}

				items.Add(new TreeViewItem {
					id = items.Count,
					depth = depth,
					displayName = isCurrent ? "This" : $"{child.name} ({child.folderName})"
				});

				this._creatorItems.Add(child);
				this._visitedCreators.Add(child);
				if (isCurrent) continue;
				this.TraverseCreator(depth + 1, items, child);
			}
		}

		protected override void RowGUI(RowGUIArgs args) {
			if (args.item.id == this._currentCreatorIndex) {
				Rect backGroundRect = args.rowRect;
				GUI.DrawTexture(backGroundRect, this._currentBackgroundTexture, ScaleMode.StretchToFill, false, 0);
			}

			base.RowGUI(args);
		}


		private void MappingFolderCreator() {
			this._childMap = new Dictionary<ModularAvatarSubMenuCreator, List<ModularAvatarSubMenuCreator>>();
			this._rootCreators = new List<ModularAvatarSubMenuCreator>();

			foreach (ModularAvatarSubMenuCreator creator in this.Avatar.gameObject.GetComponentsInChildren<ModularAvatarSubMenuCreator>()) {
				if (!creator.enabled) continue;
				if (creator.installTargetType == ModularAvatarSubMenuCreator.InstallTargetType.VRCExpressionMenu) {
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
	}
}