using UnityEditor;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(ModularAvatarMeshSettings.InheritMode))]
    class MeshSettingsInheritMode : EnumDrawer<ModularAvatarMeshSettings.InheritMode>
    {
        protected override string localizationPrefix => "mesh_settings.inherit_mode";
    }

    [CustomEditor(typeof(ModularAvatarMeshSettings))]
    [CanEditMultipleObjects]
    internal class MeshSettingsEditor : MAEditorBase
    {
        private SerializedProperty _prop_inherit_probe_anchor;
        private SerializedProperty _prop_probe_anchor;

        private SerializedProperty _prop_inherit_bounds;
        private SerializedProperty _prop_root_bone;
        private SerializedProperty _prop_bounds;

        private void OnEnable()
        {
            _prop_inherit_probe_anchor =
                serializedObject.FindProperty(nameof(ModularAvatarMeshSettings.InheritProbeAnchor));
            _prop_probe_anchor = serializedObject.FindProperty(nameof(ModularAvatarMeshSettings.ProbeAnchor));

            _prop_inherit_bounds = serializedObject.FindProperty(nameof(ModularAvatarMeshSettings.InheritBounds));
            _prop_root_bone = serializedObject.FindProperty(nameof(ModularAvatarMeshSettings.RootBone));
            _prop_bounds = serializedObject.FindProperty(nameof(ModularAvatarMeshSettings.Bounds));
        }

        protected override void OnInnerInspectorGUI()
        {
            MeshSettingsPass.MergedSettings merged = new MeshSettingsPass.MergedSettings();
            bool haveMerged = false;

            ModularAvatarMeshSettings settings = null;
            if (targets.Length == 1)
            {
                settings = (ModularAvatarMeshSettings) target;
                var avatarTransform = RuntimeUtil.FindAvatarTransformInParents(settings.transform);
                if (avatarTransform != null)
                {
                    Component mesh = (Component) target;
                    merged = MeshSettingsPass.MergeSettings(avatarTransform, mesh.transform);
                    haveMerged = true;
                }
            }

            serializedObject.Update();

            EditorGUILayout.LabelField(G("mesh_settings.header_probe_anchor"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_prop_inherit_probe_anchor, G("mesh_settings.inherit_probe_anchor"));
            if (_prop_inherit_probe_anchor.enumValueIndex is (int) ModularAvatarMeshSettings.InheritMode.Set or (int) ModularAvatarMeshSettings.InheritMode.SetOrInherit)
            {
                EditorGUILayout.PropertyField(_prop_probe_anchor, G("mesh_settings.probe_anchor"));
            }
            else if (_prop_inherit_probe_anchor.enumValueIndex == (int) ModularAvatarMeshSettings.InheritMode.Inherit &&
                     haveMerged)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(G("mesh_settings.probe_anchor"), merged.ProbeAnchor, typeof(Transform),
                        true);
                }
            }

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField(G("mesh_settings.header_bounds"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_prop_inherit_bounds, G("mesh_settings.inherit_bounds"));
            if (_prop_inherit_bounds.enumValueIndex is (int) ModularAvatarMeshSettings.InheritMode.Set or (int) ModularAvatarMeshSettings.InheritMode.SetOrInherit)
            {
                EditorGUILayout.PropertyField(_prop_root_bone, G("mesh_settings.root_bone"));
                EditorGUILayout.PropertyField(_prop_bounds, G("mesh_settings.bounds"));
            }
            else if (_prop_inherit_bounds.enumValueIndex == (int) ModularAvatarMeshSettings.InheritMode.Inherit &&
                     haveMerged)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(G("mesh_settings.root_bone"), merged.RootBone, typeof(Transform), true);
                    EditorGUILayout.PropertyField(_prop_bounds, G("mesh_settings.bounds"));
                }
            }

            serializedObject.ApplyModifiedProperties();

            ShowLanguageUI();
        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGizmo(ModularAvatarMeshSettings component, GizmoType gizmoType)
        {
            if (component.InheritBounds != ModularAvatarMeshSettings.InheritMode.Set) return;

            Matrix4x4 oldMatrix = Gizmos.matrix;

            Vector3 center = component.Bounds.center;
            Vector3 size = component.Bounds.size;

            Transform rootBone = component.RootBone.Get(component)?.transform;
            try
            {
                if (rootBone != null)
                {
                    Gizmos.matrix *= rootBone.localToWorldMatrix;
                }

                Gizmos.DrawWireCube(center, size);
            }
            finally
            {
                Gizmos.matrix = oldMatrix;
            }
        }
    }
}