#if MA_VRCSDK3_AVATARS

using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static nadena.dev.modular_avatar.core.editor.Localization;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace nadena.dev.modular_avatar.core.editor
{
	[CustomPropertyDrawer(typeof(VRChatCollider))]
	internal class RemapColliderColliderDrawer : EnumDrawer<VRChatCollider>
	{
		protected override string localizationPrefix => "remap_vrchat_collider.vrc_collider";

		protected override Array enumValues => new object[]
		{
			VRChatCollider.FingerRingLeft,
			VRChatCollider.FingerMiddleLeft,
			VRChatCollider.FingerLittleLeft,
			VRChatCollider.FingerIndexLeft,
			VRChatCollider.FingerRingRight,
			VRChatCollider.FingerMiddleRight,
			VRChatCollider.FingerLittleRight,
			VRChatCollider.FingerIndexRight,
			VRChatCollider.HandLeft,
			VRChatCollider.HandRight,
			VRChatCollider.Head,
			VRChatCollider.Torso,
			VRChatCollider.FootLeft,
			VRChatCollider.FootRight,
		};
	}
	[CustomEditor(typeof(ModularAvatarRemapVRChatCollider))]
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
			prop_manualRemap = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.manualRemap));
			prop_colliderToRemap = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.colliderToRemap));
			remapTarget = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.remapTarget));
			prop_customShape = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.customShape));
			prop_radius = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.radius));
			prop_height = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.height));
			prop_position = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.position));
			prop_rotation = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.rotation));
			prop_visualizeGizmo = serializedObject.FindProperty(nameof(ModularAvatarRemapVRChatCollider.visualizeGizmo));
		}

		protected override void OnInnerInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(prop_manualRemap, G("remap_vrchat_collider.manual_remap"));
			//using (new EditorGUI.DisabledScope(!prop_manualRemap.boolValue))
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_colliderToRemap, (G("remap_vrchat_collider.collider_to_remap")));
			}
			else
			{
				EditorGUILayout.HelpBox(S("hint.remap_vrchat_collider_manual"), MessageType.Info);
			}
			EditorGUILayout.PropertyField(remapTarget, G("remap_vrchat_collider.remap_target"));

			/*foldout = EditorGUILayout.Foldout(foldout, G("boneproxy.foldout.advanced"));
			if (foldout)
			{
				EditorGUI.indentLevel++;
				EditorGUI.indentLevel--;
			}*/

			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(prop_customShape, G("remap_vrchat_collider.custom_shape"));
			if (prop_customShape.boolValue)
			{
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(prop_radius, G("remap_vrchat_collider.radius"));
				EditorGUILayout.PropertyField(prop_height, G("remap_vrchat_collider.height"));
				EditorGUILayout.PropertyField(prop_position, G("remap_vrchat_collider.position"));
				EditorGUILayout.PropertyField(prop_rotation, G("remap_vrchat_collider.rotation"));

				EditorGUI.indentLevel--;
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(prop_visualizeGizmo, G("remap_vrchat_collider.visualize_gizmo"));

			serializedObject.ApplyModifiedProperties();
			ShowLanguageUI();
		}

		public void OnSceneGUI()
		{
			DrawCollider();
		}


		void DrawCollider()
		{
			var my = (ModularAvatarRemapVRChatCollider)target;
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

			//If it's not a a custom shape we pull from the original config.
			if (!my.customShape)
			{
				switch (my.colliderToRemap)
				{
					case VRChatCollider.Head:
						colliderConfig = descriptor.collider_head;
						break;
					case VRChatCollider.Torso:
						colliderConfig = descriptor.collider_torso;
						break;
					case VRChatCollider.HandLeft:
						colliderConfig = descriptor.collider_handL;
						break;
					case VRChatCollider.HandRight:
						colliderConfig = descriptor.collider_handR;
						break;
					case VRChatCollider.FingerIndexLeft:
						colliderConfig = descriptor.collider_fingerIndexL;
						break;
					case VRChatCollider.FingerIndexRight:
						colliderConfig = descriptor.collider_fingerIndexR;
						break;
					case VRChatCollider.FingerMiddleLeft:
						colliderConfig = descriptor.collider_fingerMiddleL;
						break;
					case VRChatCollider.FingerMiddleRight:
						colliderConfig = descriptor.collider_fingerMiddleR;
						break;
					case VRChatCollider.FingerRingLeft:
						colliderConfig = descriptor.collider_fingerRingL;
						break;
					case VRChatCollider.FingerRingRight:
						colliderConfig = descriptor.collider_fingerRingR;
						break;
					case VRChatCollider.FingerLittleLeft:
						colliderConfig = descriptor.collider_fingerRingL;
						break;
					case VRChatCollider.FingerLittleRight:
						colliderConfig = descriptor.collider_fingerRingR;
						break;
					case VRChatCollider.FootLeft:
						colliderConfig = descriptor.collider_footL;
						break;
					case VRChatCollider.FootRight:
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