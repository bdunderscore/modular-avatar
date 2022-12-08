using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core {
	[AddComponentMenu("Modular Avatar/MA SubMenu Creator")]
	public class ModularAvatarSubMenuCreator : AvatarTagComponent {
		public InstallTargetType installTargetType;
		public VRCExpressionsMenu installTargetMenu;
		public ModularAvatarSubMenuCreator installTargetCreator;
		public string folderName;
		public Texture2D icon;

		
		public enum InstallTargetType {
			VRCExpressionMenu,
			FolderCreator,
		}
		
	}
}