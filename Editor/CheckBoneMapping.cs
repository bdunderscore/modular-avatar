using System;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class CheckBoneMappingWindow : EditorWindow
    {
        [MenuItem("Tools/anatawa12 gists/CheckBoneMappingWindow")]
        public static void Open() => GetWindow<CheckBoneMappingWindow>();

        private Animator _animator;
        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            _animator = EditorGUILayout.ObjectField("Animator", _animator, typeof(Animator), true) as Animator;

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (_animator == null || !_animator.isHuman)
            {
                EditorGUILayout.HelpBox("Please assign a valid Humanoid Animator", MessageType.Warning);
            }
            else
            {
                //_animator.GetBoneTransform()
                for (HumanBodyBones bone = 0; bone < HumanBodyBones.LastBone; bone++)
                {
                    var transform = _animator.GetBoneTransform(bone);

                    var box = EditorGUILayout.GetControlRect(true);
                    box = EditorGUI.PrefixLabel(box, new GUIContent(bone.ToString()));

                    var transformBox = box;
                    transformBox.width = EditorGUIUtility.labelWidth;
                    var infoBox = box;
                    infoBox.x += transformBox.width + 2;
                    infoBox.width = box.width - transformBox.width - 2;

                    EditorGUI.ObjectField(transformBox, transform, typeof(Transform), true);
                    if (transform != null)
                    {
                        var nameNormalized = HeuristicBoneMapper.NormalizeName(transform.name);
                        var names = HeuristicBoneMapper.BoneToNameMap[bone];
                        if (names.Contains(nameNormalized))
                            GUI.Label(infoBox, "Bone mapping is correct");
                        else
                            GUI.Label(infoBox, "Bone mapping is incorrect", EditorStyles.boldLabel);
                    }
                }
            }
            GUILayout.EndScrollView();
        }
    }
}