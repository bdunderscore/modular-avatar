#region

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;

#if MA_VRCSDK3_AVATARS
using nadena.dev.ndmf.vrchat;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
#endif

using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
	internal class GlobalColliderPass : Pass<GlobalColliderPass>
	{
		protected override void Execute(ndmf.BuildContext ctx)
		{
			var remapColliders = ctx.AvatarRootTransform.GetComponentsInChildren<ModularAvatarGlobalCollider>(true);
			if (remapColliders.Length == 0) return;

			Array.Sort(remapColliders, (a, b) =>
			{
				if (a.manualRemap && !b.manualRemap) return -1;
				if (!a.manualRemap && b.manualRemap) return 1;
				return 0;
			});

#if MA_VRCSDK3_AVATARS
			var indexFingerWarns = new List<GameObject>();
			var skippedColliders = new List<GameObject>();

			var usedColliders = new List<GlobalCollider>();
			var colliderPriority = new[]
			{
				GlobalCollider.FingerRingLeft,
				GlobalCollider.FingerRingRight,
				GlobalCollider.FingerMiddleLeft,
				GlobalCollider.FingerMiddleRight,
				GlobalCollider.FingerLittleLeft,
				GlobalCollider.FingerLittleRight,
				GlobalCollider.FingerIndexLeft,
				GlobalCollider.FingerIndexRight
			};

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
					//This ended up being overly instrusive since it's purposefully used in some of my workflows. I don't think it's needed.
					/*if (usedColliders.Contains(my.sourceCollider))
					{
						//Collider was already used, so it will be overriten by this remap.
						//BuildReport.Log(ErrorSeverity.Information, "validation.global_collider.manual_collider_overwrite", my.gameObject);
					}*/
					targetCollider = my.sourceCollider;
				}
				else
				{
					targetCollider = GlobalCollider.None;
					foreach (var collider in colliderPriority)
					{
						if (usedColliders.Contains(collider))
							continue;

						if (collider == GlobalCollider.FingerIndexLeft || collider == GlobalCollider.FingerIndexRight)
							indexFingerWarns.Add(my.gameObject);

						targetCollider = collider;
						break;
					}
					if (targetCollider == GlobalCollider.None)
					{
						skippedColliders.Add(my.gameObject);
						continue;
					}
				}
				usedColliders.Add(targetCollider);

				var desc = ctx.VRChatAvatarDescriptor();

				//It's a finger if it's not any of the other colliders.
				bool isFinger =
					!(targetCollider == GlobalCollider.Head || targetCollider == GlobalCollider.Torso ||
					  targetCollider == GlobalCollider.HandLeft || targetCollider == GlobalCollider.HandRight ||
					  targetCollider == GlobalCollider.FootLeft || targetCollider == GlobalCollider.FootRight);

				if (isFinger)
				{
					//VRC Finger bones are special, they automatically calculate their position and rotation based on their parent bone.
					//Create an empty inside the target bone which will contain offsets
					var ColliderRoot = new GameObject($"MA_ColliderRoot_{targetCollider}_{ColliderIndex}");
					ColliderRoot.transform.SetParent(remapTargetObj.transform, false);
					//Offset for collider height and apply offsets
					ColliderRoot.transform.localPosition = new Vector3(my.position.x, (my.height * -0.5f) + my.position.y, my.position.z);
					ColliderRoot.transform.localRotation = my.rotation;

					var ColliderTarget = new GameObject($"MA_ColliderTarget_{targetCollider}_{ColliderIndex}");
					ColliderTarget.transform.SetParent(ColliderRoot.transform, false);
					//Always setting y to 0.001 so the collider is in the correct orientation.
					//VRC Does not allow the height of a finger collider to be shorter than the distance from the finger tip to base.
					ColliderTarget.transform.localPosition = new Vector3(0, 0.001f, 0);
					newColliderConfig.transform = ColliderTarget.transform;
					newColliderConfig.position = Vector3.zero;
					newColliderConfig.rotation = Quaternion.identity;
				}

				//Copy original collider shape if toggled on. The editor view updates the component values but only when it's selected.
				//If a prefab is dropped onto the avatar without ever being selected or viewed, the values might be wrong.
				if (my.copyOriginalShape)
				{
					var sourceColliderConfig = new ColliderConfig();

					switch (targetCollider)
					{
						case GlobalCollider.Head:
							sourceColliderConfig = desc.collider_head;
							break;
						case GlobalCollider.Torso:
							sourceColliderConfig = desc.collider_torso;
							break;
						case GlobalCollider.HandLeft:
							sourceColliderConfig = desc.collider_handL;
							break;
						case GlobalCollider.HandRight:
							sourceColliderConfig = desc.collider_handR;
							break;
						case GlobalCollider.FingerIndexLeft:
							sourceColliderConfig = desc.collider_fingerIndexL;
							break;
						case GlobalCollider.FingerIndexRight:
							sourceColliderConfig = desc.collider_fingerIndexR;
							break;
						case GlobalCollider.FingerMiddleLeft:
							sourceColliderConfig = desc.collider_fingerMiddleL;
							break;
						case GlobalCollider.FingerMiddleRight:
							sourceColliderConfig = desc.collider_fingerMiddleR;
							break;
						case GlobalCollider.FingerRingLeft:
							sourceColliderConfig = desc.collider_fingerRingL;
							break;
						case GlobalCollider.FingerRingRight:
							sourceColliderConfig = desc.collider_fingerRingR;
							break;
						case GlobalCollider.FingerLittleLeft:
							sourceColliderConfig = desc.collider_fingerLittleL;
							break;
						case GlobalCollider.FingerLittleRight:
							sourceColliderConfig = desc.collider_fingerLittleR;
							break;
						case GlobalCollider.FootLeft:
							sourceColliderConfig = desc.collider_footL;
							break;
						case GlobalCollider.FootRight:
							sourceColliderConfig = desc.collider_footR;
							break;
					}
					newColliderConfig.radius = sourceColliderConfig.radius;
					newColliderConfig.height = sourceColliderConfig.height;
					newColliderConfig.position = sourceColliderConfig.position;
					newColliderConfig.rotation = sourceColliderConfig.rotation;
				}

				//Apply new collider config to descriptor
				switch (targetCollider)
				{
					case GlobalCollider.Head:
						desc.collider_head = newColliderConfig;
						break;
					case GlobalCollider.Torso:
						desc.collider_torso = newColliderConfig;
						break;
					case GlobalCollider.HandLeft:
						desc.collider_handL = newColliderConfig;
						desc.collider_handR.isMirrored = false;
						break;
					case GlobalCollider.HandRight:
						desc.collider_handR = newColliderConfig;
						desc.collider_handL.isMirrored = false;
						break;
					case GlobalCollider.FingerIndexLeft:
						desc.collider_fingerIndexL = newColliderConfig;
						desc.collider_fingerIndexR.isMirrored = false;
						break;
					case GlobalCollider.FingerIndexRight:
						desc.collider_fingerIndexR = newColliderConfig;
						desc.collider_fingerIndexL.isMirrored = false;
						break;
					case GlobalCollider.FingerMiddleLeft:
						desc.collider_fingerMiddleL = newColliderConfig;
						desc.collider_fingerMiddleR.isMirrored = false;
						break;
					case GlobalCollider.FingerMiddleRight:
						desc.collider_fingerMiddleR = newColliderConfig;
						desc.collider_fingerMiddleL.isMirrored = false;
						break;
					case GlobalCollider.FingerRingLeft:
						desc.collider_fingerRingL = newColliderConfig;
						desc.collider_fingerRingR.isMirrored = false;
						break;
					case GlobalCollider.FingerRingRight:
						desc.collider_fingerRingR = newColliderConfig;
						desc.collider_fingerRingL.isMirrored = false;
						break;
					case GlobalCollider.FingerLittleLeft:
						desc.collider_fingerLittleL = newColliderConfig;
						desc.collider_fingerLittleR.isMirrored = false;
						break;
					case GlobalCollider.FingerLittleRight:
						desc.collider_fingerLittleR = newColliderConfig;
						desc.collider_fingerLittleL.isMirrored = false;
						break;
					case GlobalCollider.FootLeft:
						desc.collider_footL = newColliderConfig;
						desc.collider_footR.isMirrored = false;
						break;
					case GlobalCollider.FootRight:
						desc.collider_footR = newColliderConfig;
						desc.collider_footL.isMirrored = false;
						break;
				}

				Object.DestroyImmediate(my);
			}

			if (indexFingerWarns.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.Information, "validation.global_collider.using_index_fingers_vrc", indexFingerWarns);
			}
			if (skippedColliders.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.NonFatal, "error.global_collider.no_global_colliders_available_vrc", skippedColliders);
			}
#endif
		}
	}
}
