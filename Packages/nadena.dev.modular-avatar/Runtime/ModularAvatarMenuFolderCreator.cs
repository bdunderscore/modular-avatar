using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core {
	[AddComponentMenu("Modular Avatar/MA Menu Folder Creator")]
	public class ModularAvatarMenuFolderCreator : AvatarTagComponent {
		public InstallTargetType installTargetType;
		public VRCExpressionsMenu installTargetMenu;
		public ModularAvatarMenuFolderCreator installTargetFolderCreator;
		public string folderName;

		
		public enum InstallTargetType {
			VRCExpressionMenu,
			FolderCreator,
		}
		
	}
}