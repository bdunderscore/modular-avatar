#region

using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
#endif

#endregion

namespace nadena.dev.modular_avatar.core
{
	//All available VRChat Colliders
	public enum GlobalCollider
	{
		Head,
		Torso,
		HandLeft, HandRight,
		FingerIndexLeft, FingerIndexRight,
		FingerMiddleLeft, FingerMiddleRight,
		FingerRingLeft, FingerRingRight,
		FingerLittleLeft, FingerLittleRight,
		FootLeft, FootRight,
		None,
	}

	[AddComponentMenu("Modular Avatar/MA Global Collider")]
	[HelpURL("https://modular-avatar.nadena.dev/docs/reference/global-collider?lang=auto")]
	public class ModularAvatarGlobalCollider : AvatarTagComponent
	{
		//Toggle for manual remapping, if false auto remapping.
		[SerializeField]
		internal bool m_manualRemap = false;
		public bool ManualRemap {
			get => m_manualRemap;
			set => m_manualRemap = value;
		}

		//descriptor collider to modify (Only in manual mode)
		[SerializeField]
		internal GlobalCollider m_colliderToHijack = GlobalCollider.None;
		public GlobalCollider ColliderToHijack {
			get => m_colliderToHijack;
			set => m_colliderToHijack = value;
		}

		[SerializeField]
		internal bool m_lowPriority = false;
		public bool LowPriority {
			get => m_lowPriority;
			set => m_lowPriority = value;
		}

		[SerializeField]
		internal AvatarObjectReference m_rootTransform = new();
		public Transform RootTransform {
			get
			{
				var rootObj = m_rootTransform?.Get(this);

				return rootObj?.transform != null ? rootObj.transform : transform;
			}
			set {
				if (m_rootTransform == null) m_rootTransform = new();
				m_rootTransform.Set(value?.gameObject);
			}
		}
		public GameObject remapTargetObject => RootTransform.gameObject;


		//Copy shape from descriptor collider (Only in manual mode, VRCSDK)
		[SerializeField]
		internal bool m_copyHijackedShape = false;
		public bool CopyHijackedShape {
			get => m_copyHijackedShape;
			set => m_copyHijackedShape = value;
		}
		[SerializeField]
		internal bool m_visualizeGizmo = true;
		public bool VisualizeGizmo {
			get => m_visualizeGizmo;
			set => m_visualizeGizmo = value;
		}
		[SerializeField]
		internal float m_radius = 0.05f;
		public float Radius {
			get => m_radius;
			set => m_radius = value;
		}
		[SerializeField]
		//Capsul Height is along the Y Axis.
		internal float m_height = 0.2f;
		public float Height {
			get => m_height;
			set => m_height = value;
		}
		[SerializeField]
		internal Vector3 m_position = Vector3.zero;
		public Vector3 Position {
			get => m_position;
			set => m_position = value;
		}
		[SerializeField]
		internal Quaternion m_rotation = Quaternion.identity;
		public Quaternion Rotation {
			get => m_rotation;
			set => m_rotation = value;
		}

#if MA_VRCSDK3_AVATARS
		static internal GlobalCollider[] validVRChatColliders = new[]
		{
			GlobalCollider.Head,
			GlobalCollider.Torso,
			GlobalCollider.HandLeft,
			GlobalCollider.HandRight,
			GlobalCollider.FootLeft,
			GlobalCollider.FootRight,
			GlobalCollider.FingerRingLeft,
			GlobalCollider.FingerRingRight,
			GlobalCollider.FingerMiddleLeft,
			GlobalCollider.FingerMiddleRight,
			GlobalCollider.FingerLittleLeft,
			GlobalCollider.FingerLittleRight,
			GlobalCollider.FingerIndexLeft,
			GlobalCollider.FingerIndexRight
		};

		//Returns a reference to the collider config in the descriptor.
		//also removes mirroring on related colliders.
		static internal ref ColliderConfig GetVRChatDescriptorCollider(VRCAvatarDescriptor desc, GlobalCollider collider, bool disableMirroring)
		{
			switch (collider)
			{
				case GlobalCollider.Head: return ref desc.collider_head;
				case GlobalCollider.Torso: return ref desc.collider_torso;
				case GlobalCollider.HandLeft:
					if (disableMirroring) desc.collider_handR.isMirrored = false;
					return ref desc.collider_handL;
				case GlobalCollider.HandRight:
					if (disableMirroring) desc.collider_handL.isMirrored = false;
					return ref desc.collider_handR;
				case GlobalCollider.FingerIndexLeft:
					if (disableMirroring) desc.collider_fingerIndexR.isMirrored = false;
					return ref desc.collider_fingerIndexL;
				case GlobalCollider.FingerIndexRight:
					if (disableMirroring) desc.collider_fingerIndexL.isMirrored = false;
					return ref desc.collider_fingerIndexR;
				case GlobalCollider.FingerMiddleLeft:
					if (disableMirroring) desc.collider_fingerMiddleR.isMirrored = false;
					return ref desc.collider_fingerMiddleL;
				case GlobalCollider.FingerMiddleRight:
					if (disableMirroring) desc.collider_fingerMiddleL.isMirrored = false;
					return ref desc.collider_fingerMiddleR;
				case GlobalCollider.FingerRingLeft:
					if (disableMirroring) desc.collider_fingerRingR.isMirrored = false;
					return ref desc.collider_fingerRingL;
				case GlobalCollider.FingerRingRight:
					if (disableMirroring) desc.collider_fingerRingL.isMirrored = false;
					return ref desc.collider_fingerRingR;
				case GlobalCollider.FingerLittleLeft:
					if (disableMirroring) desc.collider_fingerLittleR.isMirrored = false;
					return ref desc.collider_fingerLittleL;
				case GlobalCollider.FingerLittleRight:
					if (disableMirroring) desc.collider_fingerLittleL.isMirrored = false;
					return ref desc.collider_fingerLittleR;
				case GlobalCollider.FootLeft:
					if (disableMirroring) desc.collider_footR.isMirrored = false;
					return ref desc.collider_footL;
				case GlobalCollider.FootRight:
					if (disableMirroring) desc.collider_footL.isMirrored = false;
					return ref desc.collider_footR;
				default:
					// This should never happen.
					return ref desc.collider_head;
					// Just in case, returning something.
			}
		}
#endif
	}
}