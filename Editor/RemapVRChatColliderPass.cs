#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.vrchat;
using UnityEngine;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
	[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
	internal class GlobalColliderPass : Pass<GlobalColliderPass>
	{
		protected override void Execute(ndmf.BuildContext ctx)
		{
			var remapColliders = ctx.AvatarRootTransform.GetComponentsInChildren<ModularAvatarGlobalCollider>(true);
			if (remapColliders.Length == 0) return;

			//Platform specific branch may want to happen here.

			//Reording so manual remaps run before automatic.
			Array.Sort(remapColliders, (a, b) =>
			{
				if (a.manualRemap && !b.manualRemap) return -1;
				if (!a.manualRemap && b.manualRemap) return 1;
				return 0;
			});

			var usedColliders = new List<GlobalCollider>();

			var indexFingerWarns = new List<GameObject>();
			var skippedRemaps = new List<GameObject>();

			var ColliderIndex = 0;
			foreach (var my in remapColliders)
			{
				ColliderIndex++;
				GlobalCollider targetCollider;

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
						//BuildReport.Log(ErrorSeverity.Information, "validation.global_collider.manual_collider_overwrite", my.gameObject);
						//This ended up being instrusive since it's used in some of my workflows. Is it nessasary? 
					}
					targetCollider = my.colliderToRemap;
				}
				else
				{
					//Collider yoink priority -> Left Right -> Ring Middle Little Index
					//Maintains the "shape" of the hand if ring and middle are taken first.
					if (!usedColliders.Contains(GlobalCollider.FingerRingLeft))
					{
						targetCollider = GlobalCollider.FingerRingLeft;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerRingRight))
					{
						targetCollider = GlobalCollider.FingerRingRight;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerMiddleLeft))
					{
						targetCollider = GlobalCollider.FingerMiddleLeft;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerMiddleRight))
					{
						targetCollider = GlobalCollider.FingerMiddleRight;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerLittleLeft))
					{
						targetCollider = GlobalCollider.FingerLittleLeft;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerLittleRight))
					{
						targetCollider = GlobalCollider.FingerLittleRight;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerIndexLeft))
					{
						indexFingerWarns.Add(my.gameObject);
						targetCollider = GlobalCollider.FingerIndexLeft;
					}
					else if (!usedColliders.Contains(GlobalCollider.FingerIndexRight))
					{
						indexFingerWarns.Add(my.gameObject);
						targetCollider = GlobalCollider.FingerIndexRight;
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
						!(targetCollider == GlobalCollider.Head || targetCollider == GlobalCollider.Torso ||
						  targetCollider == GlobalCollider.HandLeft || targetCollider == GlobalCollider.HandRight ||
						  targetCollider == GlobalCollider.FootLeft || targetCollider == GlobalCollider.FootRight);

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
						case GlobalCollider.Head:
							ctx.VRChatAvatarDescriptor().collider_head = newColliderConfig;
							break;
						case GlobalCollider.Torso:
							ctx.VRChatAvatarDescriptor().collider_torso = newColliderConfig;
							break;
						case GlobalCollider.HandLeft:
							ctx.VRChatAvatarDescriptor().collider_handL = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_handR.isMirrored = false;
							break;
						case GlobalCollider.HandRight:
							ctx.VRChatAvatarDescriptor().collider_handR = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_handL.isMirrored = false;
							break;
						case GlobalCollider.FingerIndexLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerIndexL = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexR.isMirrored = false;
							break;
						case GlobalCollider.FingerIndexRight:
							ctx.VRChatAvatarDescriptor().collider_fingerIndexR = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexL.isMirrored = false;
							break;
						case GlobalCollider.FingerMiddleLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleL = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleR.isMirrored = false;
							break;
						case GlobalCollider.FingerMiddleRight:
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleR = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleL.isMirrored = false;
							break;
						case GlobalCollider.FingerRingLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerRingL = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerRingR.isMirrored = false;
							break;
						case GlobalCollider.FingerRingRight:
							ctx.VRChatAvatarDescriptor().collider_fingerRingR = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerRingL.isMirrored = false;
							break;
						case GlobalCollider.FingerLittleLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerLittleL = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleR.isMirrored = false;
							break;
						case GlobalCollider.FingerLittleRight:
							ctx.VRChatAvatarDescriptor().collider_fingerLittleR = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleL.isMirrored = false;
							break;
						case GlobalCollider.FootLeft:
							ctx.VRChatAvatarDescriptor().collider_footL = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_footR.isMirrored = false;
							break;
						case GlobalCollider.FootRight:
							ctx.VRChatAvatarDescriptor().collider_footR = newColliderConfig;
							ctx.VRChatAvatarDescriptor().collider_footL.isMirrored = false;
							break;
					}
				}
				else
				{
					var newState = ColliderConfig.State.Custom;
					var newTransform = remapTargetObj.transform;

					switch (targetCollider)
					{
						case GlobalCollider.Head:
							ctx.VRChatAvatarDescriptor().collider_head.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_head.state = newState;
							break;
						case GlobalCollider.Torso:
							ctx.VRChatAvatarDescriptor().collider_torso.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_torso.state = newState;
							break;
						case GlobalCollider.HandLeft:
							ctx.VRChatAvatarDescriptor().collider_handL.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_handL.state = newState;
							ctx.VRChatAvatarDescriptor().collider_handL.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_handR.isMirrored = false;
							break;
						case GlobalCollider.HandRight:
							ctx.VRChatAvatarDescriptor().collider_handR.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_handR.state = newState;
							ctx.VRChatAvatarDescriptor().collider_handR.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_handL.isMirrored = false;
							break;
						case GlobalCollider.FingerIndexLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerIndexL.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexL.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexL.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexR.isMirrored = false;
							break;
						case GlobalCollider.FingerIndexRight:
							ctx.VRChatAvatarDescriptor().collider_fingerIndexR.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexR.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexR.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerIndexL.isMirrored = false;
							break;
						case GlobalCollider.FingerMiddleLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleL.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleL.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleL.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleR.isMirrored = false;
							break;
						case GlobalCollider.FingerMiddleRight:
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleR.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleR.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleR.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerMiddleL.isMirrored = false;
							break;
						case GlobalCollider.FingerRingLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerRingL.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerRingL.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerRingL.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerRingR.isMirrored = false;
							break;
						case GlobalCollider.FingerRingRight:
							ctx.VRChatAvatarDescriptor().collider_fingerRingR.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerRingR.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerRingR.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerRingL.isMirrored = false;
							break;
						case GlobalCollider.FingerLittleLeft:
							ctx.VRChatAvatarDescriptor().collider_fingerLittleL.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleL.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleL.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleR.isMirrored = false;
							break;
						case GlobalCollider.FingerLittleRight:
							ctx.VRChatAvatarDescriptor().collider_fingerLittleR.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleR.state = newState;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleR.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_fingerLittleL.isMirrored = false;
							break;
						case GlobalCollider.FootLeft:
							ctx.VRChatAvatarDescriptor().collider_footL.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_footL.state = newState;
							ctx.VRChatAvatarDescriptor().collider_footL.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_footR.isMirrored = false;
							break;
						case GlobalCollider.FootRight:
							ctx.VRChatAvatarDescriptor().collider_footR.transform = newTransform;
							ctx.VRChatAvatarDescriptor().collider_footR.state = newState;
							ctx.VRChatAvatarDescriptor().collider_footR.isMirrored = false;
							ctx.VRChatAvatarDescriptor().collider_footL.isMirrored = false;
							break;
					}
				}

				Object.DestroyImmediate(my);
			}

			if (indexFingerWarns.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.Information, "validation.global_collider.using_index_fingers_vrc", indexFingerWarns);
			}
			if (skippedRemaps.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.NonFatal, "error.global_collider.no_global_colliders_available_vrc", skippedRemaps);
			}
		}
	}
}

#endif