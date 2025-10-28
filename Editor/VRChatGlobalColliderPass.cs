﻿#if MA_VRCSDK3_AVATARS
#region
using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;

using nadena.dev.ndmf.vrchat;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
#endregion

namespace nadena.dev.modular_avatar.core.editor
{
	[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
	internal class VRChatGlobalColliderPass : Pass<VRChatGlobalColliderPass>
	{
		protected override void Execute(ndmf.BuildContext ctx)
		{
			var remapColliders = ctx.AvatarRootTransform.GetComponentsInChildren<ModularAvatarGlobalCollider>(true);
			if (remapColliders.Length == 0) return;

			remapColliders = remapColliders.OrderByDescending(c => c.ManualRemap).ToArray();

			var LogRemapUsingFinger = new HashSet<GameObject>();
			var logAutoRemapsIndexFinger = new HashSet<GameObject>();
			var logAutoRemapsFailed = new HashSet<GameObject>();
			
			var usedColliders = new Dictionary<GlobalCollider, GameObject>();

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

				var newColliderConfig = new ColliderConfig {
					state = ColliderConfig.State.Custom,
					isMirrored = false,
					transform = my.RootTransform,
					radius = my.Radius,
					height = my.Height,
					position = my.Position,
					rotation = my.Rotation,
				};

				if (my.ManualRemap)
				{
					if (my.ColliderToHijack == GlobalCollider.None)
					{
						BuildReport.Log(ErrorSeverity.Information,
							"validation.global_collider.manual_collider_none",
							my.gameObject);
						continue;
					}
					if (!ModularAvatarGlobalCollider.validVRChatColliders.Contains(my.ColliderToHijack))
					{
						//using a collider that's not something on the VRC Descriptor.
						//May happen if a value is set via debug or if it's data from another platform.
						BuildReport.Log(ErrorSeverity.Error,
							"error.global_collider.invalid_manual_collider_vrc",
							my.gameObject, my.ColliderToHijack.ToString());
					}
					targetCollider = my.ColliderToHijack;
					if (!my.IgnoreOverwriteCheck && usedColliders.TryGetValue(targetCollider, out var overwrittenCollider))
					{
						//Collider was already used, so it will be overriten by this remap.
						BuildReport.Log(ErrorSeverity.NonFatal,
							"validation.global_collider.manual_collider_overwrite", 
							overwrittenCollider.gameObject, my.gameObject);
					}
				}
				else
				{
					targetCollider = GlobalCollider.None;
					foreach (var collider in colliderPriority)
					{
						if (usedColliders.ContainsKey(collider))
							continue;

						if (collider == GlobalCollider.FingerIndexLeft || collider == GlobalCollider.FingerIndexRight)
							logAutoRemapsIndexFinger.Add(my.gameObject);

						targetCollider = collider;
						break;
					}
					if (targetCollider == GlobalCollider.None)
					{
						logAutoRemapsFailed.Add(my.gameObject);
						continue;
					}
				}

				usedColliders[targetCollider] = my.gameObject;

				var desc = ctx.VRChatAvatarDescriptor();

				//It's a finger if it's not any of the other colliders.
				bool isFinger =
					!(targetCollider == GlobalCollider.Head || targetCollider == GlobalCollider.Torso ||
					  targetCollider == GlobalCollider.HandLeft || targetCollider == GlobalCollider.HandRight ||
					  targetCollider == GlobalCollider.FootLeft || targetCollider == GlobalCollider.FootRight);

				if (isFinger)
				{
					LogRemapUsingFinger.Add(my.gameObject);

					//VRC Finger bones are special, they automatically calculate their position and rotation based on their parent bone.
					//Create an empty inside the target bone which will contain offsets
					var ColliderRoot = new GameObject($"MA_ColliderRoot_{targetCollider}_{ColliderIndex}");
					ColliderRoot.transform.SetParent(my.RootTransform, false);
					//Offset for collider height and apply offsets
					ColliderRoot.transform.localPosition = new Vector3(my.Position.x, (my.Height * -0.5f) + my.Position.y, my.Position.z);
					ColliderRoot.transform.localRotation = my.Rotation;

					var ColliderTarget = new GameObject($"MA_ColliderTarget_{targetCollider}_{ColliderIndex}");
					ColliderTarget.transform.SetParent(ColliderRoot.transform, false);
					//Always setting y to 0.001 so the collider is in the correct orientation.
					//VRC Does not allow the height of a finger collider to be shorter than the distance from the finger tip to base.
					ColliderTarget.transform.localPosition = new Vector3(0, 0.001f, 0);
					newColliderConfig.transform = ColliderTarget.transform;
					newColliderConfig.position = Vector3.zero;
					newColliderConfig.rotation = Quaternion.identity;
				}

				//ref to the collider config in the descriptor to edit
				ref var descColliderToEdit = ref ModularAvatarGlobalCollider.GetVRChatDescriptorCollider(desc, targetCollider, true);

				//Copy original collider shape if toggled on. The editor view updates the component values but only when it's selected.
				//If a prefab is dropped onto the avatar without ever being selected or viewed, the values might be wrong.
				if (my.CopyHijackedShape)
				{
					var sourceColliderConfig = descColliderToEdit;

					newColliderConfig.radius = sourceColliderConfig.radius;
					newColliderConfig.height = sourceColliderConfig.height;
					newColliderConfig.position = sourceColliderConfig.position;
					newColliderConfig.rotation = sourceColliderConfig.rotation;
				}

				//Apply edits
				descColliderToEdit = newColliderConfig;

				Object.DestroyImmediate(my);
			}

			if (logAutoRemapsIndexFinger.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.Information, 
					"validation.global_collider.using_index_fingers_vrc", LogRemapUsingFinger.Count + logAutoRemapsFailed.Count, LogRemapUsingFinger, logAutoRemapsFailed);
			}
			if (logAutoRemapsFailed.Count > 0)
			{
				BuildReport.Log(ErrorSeverity.NonFatal, 
					"validation.global_collider.no_global_colliders_available_vrc", LogRemapUsingFinger.Count+logAutoRemapsFailed.Count, LogRemapUsingFinger, logAutoRemapsFailed);
			}
		}
	}
}
#endif