﻿using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    /// Remaps all animation path references based on PathMappings data.
    /// </summary>
    internal class RemapAnimationPass
    {
        private readonly VRCAvatarDescriptor _avatarDescriptor;

        public RemapAnimationPass(VRCAvatarDescriptor avatarDescriptor)
        {
            _avatarDescriptor = avatarDescriptor;
        }

        public void Process()
        {
            // TODO: Determine which animation layers actually require processing (once we have the path-analysis database)

            var layers = _avatarDescriptor.baseAnimationLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = RemapAnimationLayer(layers[i]);
            }

            layers = _avatarDescriptor.specialAnimationLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = RemapAnimationLayer(layers[i]);
            }
        }

        private VRCAvatarDescriptor.CustomAnimLayer RemapAnimationLayer(VRCAvatarDescriptor.CustomAnimLayer layer)
        {
            if (layer.animatorController == null || layer.isDefault) return layer;

            if (!Util.IsTemporaryAsset(layer.animatorController))
            {
                layer.animatorController = Util.DeepCloneAnimator(layer.animatorController);
            }

            Util.VisitMotions(layer.animatorController as AnimatorController, MapMotion);

            return layer;
        }

        private static string MapPath(EditorCurveBinding binding)
        {
            if (binding.type == typeof(Animator) && binding.path == "")
            {
                return "";
            }
            else
            {
                return PathMappings.MapPath(binding.path, binding.type == typeof(Transform));
            }
        }

        private AnimationClip MapMotion(AnimationClip clip)
        {
            AnimationClip newClip = new AnimationClip();
            newClip.name = "remapped " + clip.name;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var newBinding = binding;
                newBinding.path = MapPath(binding);
                newClip.SetCurve(newBinding.path, newBinding.type, newBinding.propertyName,
                    AnimationUtility.GetEditorCurve(clip, binding));
            }

            foreach (var objBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var newBinding = objBinding;
                newBinding.path = MapPath(objBinding);
                AnimationUtility.SetObjectReferenceCurve(newClip, newBinding,
                    AnimationUtility.GetObjectReferenceCurve(clip, objBinding));
            }

            newClip.wrapMode = clip.wrapMode;
            newClip.legacy = clip.legacy;
            newClip.frameRate = clip.frameRate;
            newClip.localBounds = clip.localBounds;
            AnimationUtility.SetAnimationClipSettings(newClip, AnimationUtility.GetAnimationClipSettings(clip));

            return newClip;
        }
    }
}