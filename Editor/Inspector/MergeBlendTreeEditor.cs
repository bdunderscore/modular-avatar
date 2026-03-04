#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarMergeBlendTree))]
    internal class MergeBlendTreeEditor : MAEditorBase
    {
        private SerializedProperty _blendTree;
        private SerializedProperty _pathMode;
        private SerializedProperty _relativePathRoot;

        // Cache for non-constant curve check
        private Motion _lastCheckedMotion;
        private bool _lastCheckResult;
        
        private void OnEnable()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _blendTree = serializedObject.FindProperty(nameof(ModularAvatarMergeBlendTree.BlendTree));
#pragma warning restore CS0618 // Type or member is obsolete
            _pathMode = serializedObject.FindProperty(nameof(ModularAvatarMergeBlendTree.PathMode));
            _relativePathRoot = serializedObject.FindProperty(nameof(ModularAvatarMergeBlendTree.RelativePathRoot));

            // Reset cache on enable
            _lastCheckedMotion = null;
            _lastCheckResult = false;
        }
        
        protected override void OnInnerInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.ObjectField(_blendTree, typeof(Motion), G("merge_blend_tree.motion"));
            EditorGUILayout.PropertyField(_pathMode, G("merge_blend_tree.path_mode"));
            if (_pathMode.enumValueIndex == (int) MergeAnimatorPathMode.Relative)
            {
                EditorGUILayout.PropertyField(_relativePathRoot, G("merge_blend_tree.relative_path_root"));
            }
            
            serializedObject.ApplyModifiedProperties();

            // Check for non-constant curves (cached)
            var component = (ModularAvatarMergeBlendTree)target;
            var motion = component.Motion;

            // Only evaluate if motion changed
            if (motion != _lastCheckedMotion)
            {
                _lastCheckedMotion = motion;
                _lastCheckResult = motion != null && HasNonConstantCurves(motion);
            }

            if (_lastCheckResult)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    S("merge_blend_tree.non_constant_curve"),
                    MessageType.Error
                );
            }

            ShowLanguageUI();
        }

        /// <summary>
        ///     Static method to check if a motion (animation clip or blend tree) contains any non-constant curves.
        ///     This is used both by the inspector and by unit tests.
        /// </summary>
        public static bool HasNonConstantCurves(Motion motion)
        {
            var visitedBlendTrees = new HashSet<BlendTree>();
            return CheckMotionForNonConstantCurves(motion, visitedBlendTrees);
        }

        private static bool CheckMotionForNonConstantCurves(Motion motion, HashSet<BlendTree> visitedBlendTrees)
        {
            if (motion == null) return false;

            if (motion is AnimationClip clip)
            {
                return CheckClipForNonConstantCurves(clip);
            }

            if (motion is BlendTree blendTree)
            {
                // Prevent infinite recursion by tracking visited blend trees
                if (visitedBlendTrees.Contains(blendTree)) return false;
                visitedBlendTrees.Add(blendTree);

                // Check all children
                foreach (var child in blendTree.children)
                {
                    if (CheckMotionForNonConstantCurves(child.motion, visitedBlendTrees))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckClipForNonConstantCurves(AnimationClip clip)
        {
            if (clip == null) return false;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;

                // Check if the curve values change over time
                for (var i = 0; i < curve.length; i++)
                {
                    var t0 = i == 0 ? -0.5f : curve[i - 1].time;
                    var t1 = curve[i].time;
                    var t2 = curve[i].time + (t1 - t0);

                    var v0 = curve.Evaluate((t0 + t1) / 2);
                    var v1 = curve.Evaluate(t1);
                    var v2 = curve.Evaluate((t1 + t2) / 2);

                    if (!Mathf.Approximately(v0, v1) || !Mathf.Approximately(v1, v2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

#endif