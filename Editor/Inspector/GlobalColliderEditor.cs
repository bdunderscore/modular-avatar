#region

using System;
using UnityEditor;
using UnityEngine;
using System.Linq;

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

		protected override Array enumValues => new object[]
		{
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
	}
	[CustomEditor(typeof(ModularAvatarGlobalCollider))]
	[CanEditMultipleObjects]
	class GlobalColliderEditor : MAEditorBase
	{
		//private bool foldout = false;
		private SerializedProperty
			prop_manualRemap,
			prop_colliderToHijack,
			prop_rootTransform,
			prop_lowpriority,
			prop_copyHijackedShape,
			prop_radius,
			prop_height,
			prop_position,
			prop_rotation,
			prop_visualizeGizmo;

		private void OnEnable()
		{
			prop_manualRemap = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_manualRemap));
			prop_colliderToHijack = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_colliderToHijack));
			prop_rootTransform = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_rootTransform));
			prop_copyHijackedShape = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_copyHijackedShape));
			prop_radius = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_radius));
			prop_height = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_height));
			prop_position = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_position));
			prop_rotation = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_rotation));
			prop_visualizeGizmo = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_visualizeGizmo));
			prop_lowpriority = serializedObject.FindProperty(nameof(ModularAvatarGlobalCollider.m_lowPriority));
		}

		protected override void OnInnerInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(prop_manualRemap, G("global_collider.manual_remap"));
			//if manual remap is enabled, copy original is just set to false
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_colliderToHijack, G("global_collider.hijack_collider"));
#if MA_VRCSDK3_AVATARS
				if (prop_colliderToHijack.enumValueIndex == (int)GlobalCollider.Head ||
					prop_colliderToHijack.enumValueIndex == (int)GlobalCollider.Torso ||
					prop_colliderToHijack.enumValueIndex == (int)GlobalCollider.FootLeft ||
					prop_colliderToHijack.enumValueIndex == (int)GlobalCollider.FootRight)
				{
					EditorGUILayout.HelpBox(S("global_collider.contact_only_vrc"), MessageType.Info);
				}
#endif
				if (prop_colliderToHijack.enumValueIndex == (int)GlobalCollider.None)
				{
					EditorGUILayout.HelpBox(S("global_collider.hijack_none"), MessageType.Info);
				}
			}
			else
			{
				prop_copyHijackedShape.boolValue = false;
				prop_lowpriority.boolValue = false;
				EditorGUILayout.HelpBox(S("hint.global_collider_manual_vrc"), MessageType.Info);
			}

			EditorGUILayout.PropertyField(prop_rootTransform, G("global_collider.root_transform"));

			//Low Prio Toggle
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_lowpriority, G("global_collider.low_priority"));
				if (prop_lowpriority.boolValue)
				{
					EditorGUILayout.HelpBox(S("hint.global_collider.low_priority"), MessageType.Info);
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField(G("global_collider.header_shape"), EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
#if MA_VRCSDK3_AVATARS
			//Disables the shape fields if copying original shape is enabled. (Field always editable outside VRCSDK)
			using (new EditorGUI.DisabledScope(prop_copyHijackedShape.boolValue && prop_manualRemap.boolValue))
			{
#endif
				EditorGUILayout.PropertyField(prop_radius, G("global_collider.radius"));
				EditorGUILayout.PropertyField(prop_height, G("global_collider.height"));
				EditorGUILayout.PropertyField(prop_position, G("global_collider.position"));
				EditorGUILayout.PropertyField(prop_rotation, G("global_collider.rotation"));
#if MA_VRCSDK3_AVATARS
			}
#endif
			if (prop_manualRemap.boolValue)
			{
				EditorGUILayout.PropertyField(prop_copyHijackedShape, G("global_collider.copy_original_shape"));
			}
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
#if MA_VRCSDK3_AVATARS
			var my = (ModularAvatarGlobalCollider)target;
			if (!(my.VisualizeGizmo)) return;
			
			if (my.CopyHijackedShape && my.ManualRemap)
			{
				CopyOriginalCollider(my);
			}

			var scale = Mathf.Max(my.RootTransform.lossyScale.x, my.RootTransform.lossyScale.y, my.RootTransform.lossyScale.z);
			var clampedRadius = Mathf.Min(my.Radius * scale, 2.5f * 0.5f) / scale;
			var clampedHeight = Mathf.Min(my.Height * scale, 2.5f) / scale;

			var globalPos = my.RootTransform.TransformPoint(my.Position);
			var globalRot = my.RootTransform.rotation * my.Rotation;
			HandlesUtil.DrawWireCapsule(globalPos, globalRot, clampedHeight * scale, clampedRadius * scale);
#endif
		}

#if MA_VRCSDK3_AVATARS
		private void CopyOriginalCollider(ModularAvatarGlobalCollider my)
		{
			var desc = RuntimeUtil.FindAvatarInParents(my.transform);
			if (desc == null) return;

			//if not a valid VRChat collider, do nothing
			if (!ModularAvatarGlobalCollider.validVRChatColliders.Contains(my.ColliderToHijack)) return;
			ColliderConfig colConfig = ModularAvatarGlobalCollider.GetVRChatDescriptorCollider(desc, my.ColliderToHijack, false);

			my.m_radius = colConfig.radius;
			my.m_height = colConfig.height;
			my.m_position = colConfig.position;
			my.m_rotation = colConfig.rotation;
		}
#endif

	}
}