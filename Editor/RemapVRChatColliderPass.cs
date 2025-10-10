#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEngine;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
	[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
	internal class RemapVRChatCollidersPass : Pass<RemapVRChatCollidersPass>
	{
		protected override void Execute(ndmf.BuildContext ctx)
		{
			var remapColliders = ctx.AvatarRootTransform.GetComponentsInChildren<ModularAvatarRemapVRChatCollider>(true);
			if (remapColliders.Length == 0) return;

			//Reording so manual remaps run before automatic.
			Array.Sort(remapColliders, (a, b) =>
			{
				if (a.manualRemap && !b.manualRemap) return -1;
				if (!a.manualRemap && b.manualRemap) return 1;
				return 0;
			});

			var usedColliders = new List<VRChatCollider>();

			var indexFingerWarns = new List<GameObject>();
			var skippedRemaps = new List<GameObject>();

			var ColliderIndex = 0;
			foreach (var my in remapColliders)
			{
				ColliderIndex++;
				VRChatCollider targetCollider;

				//If none, use gameobject component is on.
				var remapTargetObj = my.remapTargetObject ?? my.gameObject;

				var newColliderConfig = new ColliderConfig {
					state = ColliderConfig.State.Custom,
					isMirrored = false,
					transform = remapTargetObj.transform,
					radius = my.radius,
					height = my.height,
					position = my.position,
					rotation = my.rotation,
				};

				if (my.manualRemap)
				{
					if (usedColliders.Contains(my.colliderToRemap))
					{
						//Collider was already used, so it will be overriten by this remap.
						//BuildReport.Log(ErrorSeverity.Information, "validation.remap_vrchat_collider.manual_collider_overwrite", my.gameObject);
						//This ended up being instrusive since it's used in some of my workflows. Is it nessasary? 
					}
					targetCollider = my.colliderToRemap;
				}
				else
				{
					//Collider yoink priority -> Left Right -> Ring Middle Little Index
					//Maintains the "shape" of the hand if ring and middle are taken first.
					if (!usedColliders.Contains(VRChatCollider.FingerRingLeft))
					{
						targetCollider = VRChatCollider.FingerRingLeft;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerRingRight))
					{
						targetCollider = VRChatCollider.FingerRingRight;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerMiddleLeft))
					{
						targetCollider = VRChatCollider.FingerMiddleLeft;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerMiddleRight))
					{
						targetCollider = VRChatCollider.FingerMiddleRight;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerLittleLeft))
					{
						targetCollider = VRChatCollider.FingerLittleLeft;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerLittleRight))
					{
						targetCollider = VRChatCollider.FingerLittleRight;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerIndexLeft))
					{
						indexFingerWarns.Add(my.gameObject);
						targetCollider = VRChatCollider.FingerIndexLeft;
					}
					else if (!usedColliders.Contains(VRChatCollider.FingerIndexRight))
					{
						indexFingerWarns.Add(my.gameObject);
						targetCollider = VRChatCollider.FingerIndexRight;
					}
					else
					{
						skippedRemaps.Add(my.gameObject);
						continue;
					}
				}
				usedColliders.Add(targetCollider);

				if (my.customShape)
				{
					//It's a finger if it's not any of the other colliders.
					bool isFinger =
						!(targetCollider == VRChatCollider.Head || targetCollider == VRChatCollider.Torso ||
						  targetCollider == VRChatCollider.HandLeft || targetCollider == VRChatCollider.HandRight ||
						  targetCollider == VRChatCollider.FootLeft || targetCollider == VRChatCollider.FootRight);

					if (isFinger)
					{
						//Finger bones are special, they automatically calculate their position and rotation based on their parent bone.
						//Create an empty inside the target bone which will contain offsets
						var ColliderRoot = new GameObject($"MA_ColliderRoot_{targetCollider}_{ColliderIndex}");
						ColliderRoot.transform.SetParent(remapTargetObj.transform, false);
						//Offset for collider height and apply offsets
						ColliderRoot.transform.localPosition = new Vector3(my.position.x, (my.height * -0.5f) + my.position.y, my.position.z);
						ColliderRoot.transform.localRotation = my.rotation;

						var ColliderTarget = new GameObject($"MA_ColliderTarget_{targetCollider}_{ColliderIndex}");
						ColliderTarget.transform.SetParent(ColliderRoot.transform, false);
						//Always setting y to 0.1 so the collider is in the correct orientation.
						ColliderTarget.transform.localPosition = new Vector3(0, 0.1f, 0);
						newColliderConfig.transform = ColliderTarget.transform;
						newColliderConfig.position = Vector3.zero;
						newColliderConfig.rotation = Quaternion.identity;
					}

					switch (targetCollider)
					{
						case VRChatCollider.Head:
							ctx.AvatarDescriptor.collider_head = newColliderConfig;
							break;
						case VRChatCollider.Torso:
							ctx.AvatarDescriptor.collider_torso = newColliderConfig;
							break;
						case VRChatCollider.HandLeft:
							ctx.AvatarDescriptor.collider_handL = newColliderConfig;
							ctx.AvatarDescriptor.collider_handR.isMirrored = false;
							break;
						case VRChatCollider.HandRight:
							ctx.AvatarDescriptor.collider_handR = newColliderConfig;
							ctx.AvatarDescriptor.collider_handL.isMirrored = false;
							break;
						case VRChatCollider.FingerIndexLeft:
							ctx.AvatarDescriptor.collider_fingerIndexL = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerIndexR.isMirrored = false;
							break;
						case VRChatCollider.FingerIndexRight:
							ctx.AvatarDescriptor.collider_fingerIndexR = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerIndexL.isMirrored = false;
							break;
						case VRChatCollider.FingerMiddleLeft:
							ctx.AvatarDescriptor.collider_fingerMiddleL = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerMiddleR.isMirrored = false;
							break;
						case VRChatCollider.FingerMiddleRight:
							ctx.AvatarDescriptor.collider_fingerMiddleR = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerMiddleL.isMirrored = false;
							break;
						case VRChatCollider.FingerRingLeft:
							ctx.AvatarDescriptor.collider_fingerRingL = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerRingR.isMirrored = false;
							break;
						case VRChatCollider.FingerRingRight:
							ctx.AvatarDescriptor.collider_fingerRingR = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerRingL.isMirrored = false;
							break;
						case VRChatCollider.FingerLittleLeft:
							ctx.AvatarDescriptor.collider_fingerLittleL = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerLittleR.isMirrored = false;
							break;
						case VRChatCollider.FingerLittleRight:
							ctx.AvatarDescriptor.collider_fingerLittleR = newColliderConfig;
							ctx.AvatarDescriptor.collider_fingerLittleL.isMirrored = false;
							break;
						case VRChatCollider.FootLeft:
							ctx.AvatarDescriptor.collider_footL = newColliderConfig;
							ctx.AvatarDescriptor.collider_footR.isMirrored = false;
							break;
						case VRChatCollider.FootRight:
							ctx.AvatarDescriptor.collider_footR = newColliderConfig;
							ctx.AvatarDescriptor.collider_footL.isMirrored = false;
							break;
					}
				}
				else
				{
					var newState = ColliderConfig.State.Custom;
					var newTransform = remapTargetObj.transform;

					switch (targetCollider)
					{
						case VRChatCollider.Head:
							ctx.AvatarDescriptor.collider_head.transform = newTransform;
							ctx.AvatarDescriptor.collider_head.state = newState;
							break;
						case VRChatCollider.Torso:
							ctx.AvatarDescriptor.collider_torso.transform = newTransform;
							ctx.AvatarDescriptor.collider_torso.state = newState;
							break;
						case VRChatCollider.HandLeft:
							ctx.AvatarDescriptor.collider_handL.transform = newTransform;
							ctx.AvatarDescriptor.collider_handL.state = newState;
							ctx.AvatarDescriptor.collider_handL.isMirrored = false;
							ctx.AvatarDescriptor.collider_handR.isMirrored = false;
							break;
						case VRChatCollider.HandRight:
							ctx.AvatarDescriptor.collider_handR.transform = newTransform;
							ctx.AvatarDescriptor.collider_handR.state = newState;
							ctx.AvatarDescriptor.collider_handR.isMirrored = false;
							ctx.AvatarDescriptor.collider_handL.isMirrored = false;
							break;
						case VRChatCollider.FingerIndexLeft:
							ctx.AvatarDescriptor.collider_fingerIndexL.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerIndexL.state = newState;
							ctx.AvatarDescriptor.collider_fingerIndexL.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerIndexR.isMirrored = false;
							break;
						case VRChatCollider.FingerIndexRight:
							ctx.AvatarDescriptor.collider_fingerIndexR.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerIndexR.state = newState;
							ctx.AvatarDescriptor.collider_fingerIndexR.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerIndexL.isMirrored = false;
							break;
						case VRChatCollider.FingerMiddleLeft:
							ctx.AvatarDescriptor.collider_fingerMiddleL.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerMiddleL.state = newState;
							ctx.AvatarDescriptor.collider_fingerMiddleL.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerMiddleR.isMirrored = false;
							break;
						case VRChatCollider.FingerMiddleRight:
							ctx.AvatarDescriptor.collider_fingerMiddleR.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerMiddleR.state = newState;
							ctx.AvatarDescriptor.collider_fingerMiddleR.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerMiddleL.isMirrored = false;
							break;
						case VRChatCollider.FingerRingLeft:
							ctx.AvatarDescriptor.collider_fingerRingL.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerRingL.state = newState;
							ctx.AvatarDescriptor.collider_fingerRingL.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerRingR.isMirrored = false;
							break;
						case VRChatCollider.FingerRingRight:
							ctx.AvatarDescriptor.collider_fingerRingR.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerRingR.state = newState;
							ctx.AvatarDescriptor.collider_fingerRingR.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerRingL.isMirrored = false;
							break;
						case VRChatCollider.FingerLittleLeft:
							ctx.AvatarDescriptor.collider_fingerLittleL.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerLittleL.state = newState;
							ctx.AvatarDescriptor.collider_fingerLittleL.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerLittleR.isMirrored = false;
							break;
						case VRChatCollider.FingerLittleRight:
							ctx.AvatarDescriptor.collider_fingerLittleR.transform = newTransform;
							ctx.AvatarDescriptor.collider_fingerLittleR.state = newState;
							ctx.AvatarDescriptor.collider_fingerLittleR.isMirrored = false;
							ctx.AvatarDescriptor.collider_fingerLittleL.isMirrored = false;
							break;
						case VRChatCollider.FootLeft:
							ctx.AvatarDescriptor.collider_footL.transform = newTransform;
							ctx.AvatarDescriptor.collider_footL.state = newState;
							ctx.AvatarDescriptor.collider_footL.isMirrored = false;
							ctx.AvatarDescriptor.collider_footR.isMirrored = false;
							break;
						case VRChatCollider.FootRight:
							ctx.AvatarDescriptor.collider_footR.transform = newTransform;
							ctx.AvatarDescriptor.collider_footR.state = newState;
							ctx.AvatarDescriptor.collider_footR.isMirrored = false;
							ctx.AvatarDescriptor.collider_footL.isMirrored = false;
							break;
					}
				}

				Object.DestroyImmediate(my);
			}

			if (indexFingerWarns.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.Information, "validation.remap_vrchat_collider.using_index_fingers", indexFingerWarns);
			}
			if (skippedRemaps.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.NonFatal, "error.remap_vrchat_collider.no_global_colliders_available", skippedRemaps);
			}
		}
	}
}

#endif