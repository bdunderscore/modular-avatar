#region

using System;
using UnityEditor;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
#endif

using static nadena.dev.modular_avatar.core.editor.Localization;

#endregion


namespace nadena.dev.modular_avatar.core.editor
{
	[CustomPropertyDrawer(typeof(GlobalCollider))]
	internal class GlobalColliderColliderDrawer : EnumDrawer<GlobalCollider>
	{
		protected override string localizationPrefix => "global_collider.bone";

#if MA_VRCSDK3_AVATARS
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
		};
#endif
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
			prop_copyOriginalShape,
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
			prop_copyOriginalShape = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.copyOriginalShape));
			prop_radius = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.radius));
			prop_height = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.height));
			prop_position = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.position));
			prop_rotation = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.rotation));
			prop_visualizeGizmo = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.visualizeGizmo));
		}

		protected override void OnInnerInspectorGUI()
		{
			serializedObject.Update();

#if MA_VRCSDK3_AVATARS
			EditorGUILayout.PropertyField(prop_manualRemap, G("global_collider.manual_remap"));
			//if manual remap is enabled, copy original needs to be set to false
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_colliderToRemap, (G("global_collider.collider_to_remap")));
				if (prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.Head ||
					prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.Torso ||
					prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.FootLeft ||
					prop_colliderToRemap.enumValueIndex == (int)GlobalCollider.FootRight)
					{
						EditorGUILayout.HelpBox(S("global_collider.contact_only_vrc"), MessageType.Info);
					}
			}
			else
			{
				//Disable copy original if manual remap is off
				prop_copyOriginalShape.boolValue = false;
				EditorGUILayout.HelpBox(S("hint.global_collider_manual_vrc"), MessageType.Info);
			}
#endif
			EditorGUILayout.PropertyField(remapTarget, G("global_collider.remap_target"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField(G("global_collider.header_shape"), EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
#if MA_VRCSDK3_AVATARS
			using (new EditorGUI.DisabledScope(prop_copyOriginalShape.boolValue && prop_manualRemap.boolValue))
			{
#endif
				EditorGUILayout.PropertyField(prop_radius, G("global_collider.radius"));
				EditorGUILayout.PropertyField(prop_height, G("global_collider.height"));
				EditorGUILayout.PropertyField(prop_position, G("global_collider.position"));
				EditorGUILayout.PropertyField(prop_rotation, G("global_collider.rotation"));
#if MA_VRCSDK3_AVATARS
			}
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_copyOriginalShape, G("global_collider.copy_original_shape"));
			}
#endif
			EditorGUI.indentLevel--;

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

#if MA_VRCSDK3_AVATARS
			//If it's not a a custom shape we'll pull from the original config.
			var descriptor = remapTargetObj.GetComponentInParent<VRCAvatarDescriptor>();
			if (descriptor == null) return;
			if (my.copyOriginalShape)
			{
				ColliderConfig colliderConfig = new ColliderConfig {
					state = ColliderConfig.State.Custom,
					isMirrored = false,
					transform = remapTargetObj.transform,
					radius = my.radius,
					height = my.height,
					position = my.position,
					rotation = my.rotation
				};
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
				my.radius = colliderConfig.radius;
				my.height = colliderConfig.height;
				my.position = colliderConfig.position;
				my.rotation = colliderConfig.rotation;
			}
#endif

			var scale = Mathf.Max(my.transform.lossyScale.x, my.transform.lossyScale.y, my.transform.lossyScale.z);
			var clampedRadius = Mathf.Min(my.radius * scale, 2.5f * 0.5f) / scale;
			var clampedHeight = Mathf.Min(my.height * scale, 2.5f) / scale;

			var globalPos = my.transform.TransformPoint(my.position);
			var globalRot = my.transform.rotation * my.rotation;
			HandlesUtil.DrawWireCapsule(globalPos, globalRot, clampedHeight * scale, clampedRadius * scale);
		}

	}
}