#if MA_VRCSDK3_AVATARS

using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
	//All available VRChat Colliders
	public enum VRChatCollider
	{
		Head,
		Torso,
		HandLeft, HandRight,
		FingerIndexLeft, FingerIndexRight,
		FingerMiddleLeft, FingerMiddleRight,
		FingerRingLeft, FingerRingRight,
		FingerLittleLeft, FingerLittleRight,
		FootLeft, FootRight,
	}

	[AddComponentMenu("Modular Avatar/MA Remap VRChat Collider")]
	[HelpURL("https://modular-avatar.nadena.dev/docs/reference/remap-vrchat-collider?lang=auto")]
	public class ModularAvatarRemapVRChatCollider : AvatarTagComponent
	{
		//Toggle for manual remapping, if false auto remapping.
		public bool manualRemap = false;

		//VRChat descriptor Collider to replace (Only used in manual mode)
		public VRChatCollider colliderToRemap;

		//May be better to do this with the method bone proxy uses, that way we can provide an advanced dropdown?
		public AvatarObjectReference remapTarget = new AvatarObjectReference();
		public GameObject remapTargetObject => remapTarget.Get(this);

		// Custom Collider Shape
		public bool customShape = false;
		public bool visualizeGizmo = true;
		public float radius = 0.05f;
		public float height = 0.2f;
		public Vector3 position = Vector3.zero;
		public Quaternion rotation = Quaternion.identity;
	}
}

#endif