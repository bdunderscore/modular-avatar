#if MA_VRCSDK3_AVATARS

using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static nadena.dev.modular_avatar.core.editor.Localization;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace nadena.dev.modular_avatar.core.editor
{
	[CustomPropertyDrawer(typeof(GlobalCollider))]
	internal class GlobalColliderColliderDrawer : EnumDrawer<GlobalCollider>
	{
		protected override string localizationPrefix => "global_collider.bone";

		protected override Array enumValues => new object[]
		{
			//VRChat
			GlobalCollider.FingerRingLeft,
			GlobalCollider.FingerMiddleLeft,
			GlobalCollider.FingerLittleLeft,
			GlobalCollider.FingerIndexLeft,
			GlobalCollider.FingerRingRight,
			GlobalCollider.FingerMiddleRight,
			GlobalCollider.FingerLittleRight,
			GlobalCollider.FingerIndexRight,
			GlobalCollider.HandLeft,
			GlobalCollider.HandRight,
			GlobalCollider.Head,
			GlobalCollider.Torso,
			GlobalCollider.FootLeft,
			GlobalCollider.FootRight,

			//Resonite
		};
	}
	[CustomEditor(typeof(ModularAvatarGlobalCollider))]
	[CanEditMultipleObjects]
	class RemapVRChatColliderEditor : MAEditorBase
	{
		//private bool foldout = false;

		private SerializedProperty
			prop_manualRemap,
			prop_colliderToRemap,
			remapTarget,
			prop_customShape,
			prop_radius,
			prop_height,
			prop_position,
			prop_rotation,
			prop_visualizeGizmo;

		private void OnEnable()
		{
			prop_manualRemap = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.manualRemap));
			prop_colliderToRemap = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.colliderToRemap));
			remapTarget = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.remapTarget));
			prop_customShape = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.customShape));
			prop_radius = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.radius));
			prop_height = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.height));
			prop_position = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.position));
			prop_rotation = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.rotation));
			prop_visualizeGizmo = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.visualizeGizmo));
		}

		protected override void OnInnerInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(prop_manualRemap, G("global_collider.manual_remap"));
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_colliderToRemap, (G("global_collider.collider_to_remap")));

				//(VRC Specific) if using a collider that's purely a contact.
				if (prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.Head ||
					prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.Torso ||
					prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.FootLeft ||
					prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.FootRight)
					{
						EditorGUILayout.HelpBox(S("global_collider.contact_only"), MessageType.Info);
					}
			}
			else
			{
				EditorGUILayout.HelpBox(S("hint.global_collider_manual"), MessageType.Info);
			}
			EditorGUILayout.PropertyField(remapTarget, G("global_collider.remap_target"));

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(prop_customShape, G("global_collider.custom_shape"));
			if (prop_customShape.boolValue)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(prop_radius, G("global_collider.radius"));
				EditorGUILayout.PropertyField(prop_height, G("global_collider.height"));
				EditorGUILayout.PropertyField(prop_position, G("global_collider.position"));
				EditorGUILayout.PropertyField(prop_rotation, G("global_collider.rotation"));

				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(prop_visualizeGizmo, G("global_collider.visualize_gizmo"));

			serializedObject.ApplyModifiedProperties();
			ShowLanguageUI();
		}

		public void OnSceneGUI()
		{
			DrawCollider();
		}


		//TODO/Wishlist: It'd be great to have an editor for the collider.
		void DrawCollider()
		{
			var my = (ModularAvatarGlobalCollider)target;
			if (!(my.visualizeGizmo)) return;

			//If none, use gameobject component is on.
			var remapTargetObj = my.remapTargetObject ?? my.gameObject;

			var descriptor = remapTargetObj.GetComponentInParent<VRCAvatarDescriptor>();
			if (descriptor == null) return;

			ColliderConfig colliderConfig = new ColliderConfig {
				state = ColliderConfig.State.Custom,
				isMirrored = false,
				transform = remapTargetObj.transform,
				radius = my.radius,
				height = my.height,
				position = my.position,
				rotation = my.rotation
			};

			//If it's not a a custom shape we'll pull from the original config.
			if (!my.customShape)
			{
				switch (my.colliderToRemap)
				{
					case GlobalCollider.Head:
						colliderConfig = descriptor.collider_head;
						break;
					case GlobalCollider.Torso:
						colliderConfig = descriptor.collider_torso;
						break;
					case GlobalCollider.HandLeft:
						colliderConfig = descriptor.collider_handL;
						break;
					case GlobalCollider.HandRight:
						colliderConfig = descriptor.collider_handR;
						break;
					case GlobalCollider.FingerIndexLeft:
						colliderConfig = descriptor.collider_fingerIndexL;
						break;
					case GlobalCollider.FingerIndexRight:
						colliderConfig = descriptor.collider_fingerIndexR;
						break;
					case GlobalCollider.FingerMiddleLeft:
						colliderConfig = descriptor.collider_fingerMiddleL;
						break;
					case GlobalCollider.FingerMiddleRight:
						colliderConfig = descriptor.collider_fingerMiddleR;
						break;
					case GlobalCollider.FingerRingLeft:
						colliderConfig = descriptor.collider_fingerRingL;
						break;
					case GlobalCollider.FingerRingRight:
						colliderConfig = descriptor.collider_fingerRingR;
						break;
					case GlobalCollider.FingerLittleLeft:
						colliderConfig = descriptor.collider_fingerRingL;
						break;
					case GlobalCollider.FingerLittleRight:
						colliderConfig = descriptor.collider_fingerRingR;
						break;
					case GlobalCollider.FootLeft:
						colliderConfig = descriptor.collider_footL;
						break;
					case GlobalCollider.FootRight:
						colliderConfig = descriptor.collider_footR;
						break;
					default:
						break;
				}
				colliderConfig.transform = remapTargetObj.transform;
			}

			var transform = colliderConfig.transform;
			var radius = colliderConfig.radius;
			var height = colliderConfig.height;
			var position = colliderConfig.position;
			var rotation = colliderConfig.rotation;

			var scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
			var clampedRadius = Mathf.Min(radius * scale, 2.5f * 0.5f) / scale;
			var clampedHeight = Mathf.Min(height * scale, 2.5f) / scale;

			var globalPos = transform.TransformPoint(position);
			var globalRot = transform.rotation * rotation;
			HandlesUtil.DrawWireCapsule(globalPos, globalRot, clampedHeight * scale, clampedRadius * scale);
		}

	}
}

#endif