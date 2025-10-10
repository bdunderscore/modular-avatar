#region

using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core
{
	//All available VRChat Colliders
	public enum GlobalCollider
	{
		FingerRingLeft, FingerRingRight,
		FingerMiddleLeft, FingerMiddleRight,
		FingerLittleLeft, FingerLittleRight,
		FingerIndexLeft, FingerIndexRight,
		HandLeft, HandRight,
		Head,
		Torso,
		FootLeft, FootRight,
	}

	[AddComponentMenu("Modular Avatar/MA Global Collider")]
	[HelpURL("https://modular-avatar.nadena.dev/docs/reference/global-collider?lang=auto")]
	public class ModularAvatarGlobalCollider : AvatarTagComponent
	{
		//Toggle for manual remapping, if false auto remapping.
		public bool manualRemap = false;

		//descriptor collider to replace (Only used in manual mode)
		public GlobalCollider colliderToRemap;

		//May be better to do this with the method bone proxy uses
		public AvatarObjectReference remapTarget = new AvatarObjectReference();
		public GameObject remapTargetObject => remapTarget.Get(this);

		public bool copyOriginalShape = false;
		public bool visualizeGizmo = true;
		public float radius = 0.05f;
		public float height = 0.2f;
		public Vector3 position = Vector3.zero;
		public Quaternion rotation = Quaternion.identity;
	}
}