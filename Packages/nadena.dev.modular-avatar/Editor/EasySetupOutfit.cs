﻿using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    public class EasySetupOutfit
    {
        private const int PRIORITY = 49;

        [MenuItem("GameObject/ModularAvatar/Setup Outfit", false, PRIORITY)]
        static void SetupOutfit(MenuCommand cmd)
        {
            if (!FindBones(cmd.context,
                    out var avatarRoot, out var avatarHips, out var outfitHips)
               ) return;

            var avatarArmature = avatarHips.transform.parent;
            var outfitArmature = outfitHips.transform.parent;

            if (outfitArmature.GetComponent<ModularAvatarMergeArmature>() == null)
            {
                var merge = Undo.AddComponent<ModularAvatarMergeArmature>(outfitArmature.gameObject);
                merge.mergeTarget = new AvatarObjectReference();
                merge.mergeTarget.referencePath = RuntimeUtil.RelativePath(avatarRoot, avatarArmature.gameObject);
                merge.InferPrefixSuffix();
                HeuristicBoneMapper.RenameBonesByHeuristic(merge);
            }
        }

        [MenuItem("GameObject/ModularAvatar/Setup Outfit", true, PRIORITY)]
        static bool ValidateSetupOutfit()
        {
            if (Selection.objects.Length == 0) return false;

            foreach (var obj in Selection.objects)
            {
                if (!(obj is GameObject gameObj)) return false;
                var xform = gameObj.transform;

                if (!FindBones(obj, out var _, out var _, out var outfitHips))
                {
                    return false;
                }

                // Some users have been accidentally running Setup Outfit on the avatar itself, and/or nesting avatar
                // descriptors when transplanting outfits. Block this (and require that there be only one avdesc) by
                // refusing to run if we detect multiple avatar descriptors above the current object (or if we're run on
                // the avdesc object itself)
                var nearestAvatar = RuntimeUtil.FindAvatarInParents(xform);
                if (nearestAvatar == null || nearestAvatar.transform == xform) return false;

                var parent = nearestAvatar.transform.parent;
                if (parent != null && RuntimeUtil.FindAvatarInParents(parent) != null) return false;
            }

            return true;
        }

        private static bool FindBones(Object obj, out GameObject avatarRoot, out GameObject avatarHips,
            out GameObject outfitHips)
        {
            avatarHips = outfitHips = null;
            var outfitRoot = obj as GameObject;
            avatarRoot = outfitRoot != null
                ? RuntimeUtil.FindAvatarInParents(outfitRoot.transform)?.gameObject
                : null;
            if (outfitRoot == null || avatarRoot == null) return false;

            var avatarAnimator = avatarRoot.GetComponent<Animator>();
            if (avatarAnimator == null) return false;

            avatarHips = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject;
            if (avatarHips == null) return false;

            var outfitAnimator = outfitRoot.GetComponent<Animator>();
            if (outfitAnimator != null)
            {
                outfitHips = outfitAnimator.GetBoneTransform(HumanBodyBones.Hips)?.gameObject;
            }

            if (outfitHips == null)
            {
                // Heuristic search - usually there'll be root -> Armature -> (single child) Hips
                foreach (Transform child in outfitRoot.transform)
                {
                    foreach (Transform tempHip in child)
                    {
                        if (tempHip.name.Contains(avatarHips.name))
                        {
                            outfitHips = tempHip.gameObject;
                        }
                    }
                }
            }

            return avatarHips != null && outfitHips != null;
        }
    }
}