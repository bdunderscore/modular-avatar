using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class MergeArmaturePositionResetOptions
    {
        public bool ConvertATPose = true;
        public bool AdjustRotation;
        public bool AdjustScale;
        public bool HeuristicRootScale = true;
    }

    internal static class MergeArmatureInspectorTools
    {
        internal static bool HasValidTarget(ModularAvatarMergeArmature mergeArmature)
        {
            return mergeArmature != null && mergeArmature.mergeTarget?.Get(mergeArmature) != null;
        }

        internal static bool HasValidTarget(Transform armatureRoot)
        {
            return armatureRoot != null
                   && armatureRoot.GetComponentsInChildren<ModularAvatarMergeArmature>(true)
                       .Any(HasValidTarget);
        }

        internal static void AdjustNames(Transform armatureRoot)
        {
            if (armatureRoot == null) return;

            var primaryMergeArmature = armatureRoot.GetComponent<ModularAvatarMergeArmature>();
            if (!HasValidTarget(primaryMergeArmature))
            {
                primaryMergeArmature = armatureRoot
                    .GetComponentsInChildren<ModularAvatarMergeArmature>(true)
                    .FirstOrDefault(HasValidTarget);
            }

            AdjustNames(primaryMergeArmature);
        }

        internal static void AdjustNames(ModularAvatarMergeArmature mergeArmature)
        {
            if (!HasValidTarget(mergeArmature)) return;

            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(
                mergeArmature.mergeTarget.Get(mergeArmature).transform);
            var avatarAnimator = avatarRoot != null ? avatarRoot.GetComponent<Animator>() : null;

            // Search Outfit Root Animator
            var outfitRoot = mergeArmature.transform;
            Animator outfitAnimator = null;
            while (outfitRoot != null)
            {
                if (outfitRoot == avatarRoot)
                {
                    outfitAnimator = null;
                    break;
                }

                outfitAnimator = outfitRoot.GetComponent<Animator>();
                if (outfitAnimator != null && outfitAnimator.isHuman) break;
                outfitAnimator = null;
                outfitRoot = outfitRoot.parent;
            }

            var outfitHumanoidBones = SetupOutfit.GetOutfitHumanoidBones(outfitRoot, outfitAnimator);
            HeuristicBoneMapper.RenameBonesByHeuristic(mergeArmature,
                outfitHumanoidBones: outfitHumanoidBones, avatarAnimator: avatarAnimator);
        }

        internal static void ForcePositionToBaseAvatar(
            ModularAvatarMergeArmature mergeArmature,
            MergeArmaturePositionResetOptions options,
            bool suppressRootScale = false)
        {
            if (!HasValidTarget(mergeArmature)) return;

            var mergeTarget = mergeArmature.mergeTarget.Get(mergeArmature);
            var avatarRoot = RuntimeUtil.FindAvatarTransformInParents(mergeTarget.transform);
            var rootAnimator = avatarRoot != null ? avatarRoot.GetComponent<Animator>() : null;
            if (rootAnimator == null) return;

            var boneToTransform = new Dictionary<HumanBodyBones, Transform>();

            if (rootAnimator.isHuman)
            {
                foreach (var bone in Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>())
                {
                    if (bone == HumanBodyBones.LastBone) continue;

                    var transform = rootAnimator.GetBoneTransform(bone);
                    if (transform != null) boneToTransform[bone] = transform;
                }
            }

            if (options.ConvertATPose)
            {
                SetupOutfit.FixAPose(avatarRoot.gameObject, mergeArmature.transform, false);
            }

            if (options.HeuristicRootScale && !suppressRootScale)
            {
                AdjustRootScale();
            }

            try
            {
                Walk(mergeArmature.transform, mergeTarget.transform);
            }
            finally
            {
                mergeArmature.ResetArmatureLock();
            }

            void AdjustRootScale()
            {
                // Adjust the overall scale of the avatar based on wingspan (arm length)
                if (!boneToTransform.TryGetValue(HumanBodyBones.LeftHand, out var targetHand)) return;

                var handPath = RuntimeUtil.RelativePath(mergeTarget, targetHand.gameObject);
                handPath = string.Join("/", handPath.Split('/')
                    .Select(elem => mergeArmature.prefix + elem + mergeArmature.suffix));

                var mergeHand = mergeArmature.transform.Find(handPath);
                if (mergeHand == null) return;

var targetWingspan = Mathf.Abs(rootAnimator.transform.InverseTransformPoint(targetHand.position).x);
var mergeWingspan = Mathf.Abs(rootAnimator.transform.InverseTransformPoint(mergeHand.position).x);
if (mergeWingspan <= Mathf.Epsilon) return;
mergeArmature.transform.localScale *= targetWingspan / mergeWingspan;
            }

            void Walk(Transform mergeTransform, Transform targetTransform)
            {
                Undo.RecordObject(mergeTransform, "Merge Armature: Force outfit position");

                Debug.Log("Merge: " + mergeTransform.gameObject.name + " => " + targetTransform.gameObject.name);

                mergeTransform.position = targetTransform.position;
                if (options.AdjustScale)
                {
                    if (!options.HeuristicRootScale || mergeTransform != mergeArmature.transform)
                    {
                        mergeTransform.localScale = targetTransform.localScale;
                    }
                }

                if (options.AdjustRotation)
                {
                    mergeTransform.localRotation = targetTransform.localRotation;
                }

                var traversalQueue = new Queue<Transform>();
                traversalQueue.Enqueue(mergeTransform);

                while (traversalQueue.Count > 0)
                {
                    foreach (Transform child in traversalQueue.Dequeue())
                    {
var childMergeArmature = child.GetComponent<ModularAvatarMergeArmature>();
if (childMergeArmature != null)
{
    continue;
}

                        if (TryMatchChildBone(targetTransform, child, out var targetChild))
                        {
                            Walk(child, targetChild);
                        }
                    }
                }
            }

            bool TryMatchChildBone(Transform targetTransform, Transform child, out Transform targetChild)
            {
                var childName = child.gameObject.name;
                targetChild = null;

                if (childName.StartsWith(mergeArmature.prefix) && childName.EndsWith(mergeArmature.suffix))
                {
                    var targetObjectName = childName.Substring(mergeArmature.prefix.Length,
                        childName.Length - mergeArmature.prefix.Length - mergeArmature.suffix.Length);
                    targetChild = targetTransform.Find(targetObjectName);
                }

                return targetChild != null;
            }
        }

        internal static void MatchScaleAdjusters(ModularAvatarMergeArmature mergeArmature)
        {
            if (!HasValidTarget(mergeArmature)) return;

            MatchScaleAdjuster(mergeArmature.mergeTarget.Get(mergeArmature).transform,
                mergeArmature.transform);

            foreach (var (avatarBone, outfitBone) in mergeArmature.GetBonesMapping())
            {
                MatchScaleAdjuster(avatarBone, outfitBone);
            }

            static void MatchScaleAdjuster(Transform avatarBone, Transform outfitBone)
            {
                var avatarScaleAdjuster = avatarBone.GetComponent<ModularAvatarScaleAdjuster>();
                var outfitScaleAdjuster = outfitBone.GetComponent<ModularAvatarScaleAdjuster>();

                if (avatarScaleAdjuster == null)
                {
                    if (outfitScaleAdjuster != null)
                    {
                        Undo.DestroyObjectImmediate(outfitScaleAdjuster);
                        EditorUtility.SetDirty(outfitBone.gameObject);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(outfitBone.gameObject);
                    }

                    return;
                }

                if (outfitScaleAdjuster == null)
                {
                    outfitScaleAdjuster = Undo.AddComponent<ModularAvatarScaleAdjuster>(outfitBone.gameObject);
                }
                else
                {
                    Undo.RecordObject(outfitScaleAdjuster, "Match outfit Scale Adjuster");
                }

                outfitScaleAdjuster.Scale = avatarScaleAdjuster.Scale;
                EditorUtility.SetDirty(outfitScaleAdjuster);
                PrefabUtility.RecordPrefabInstancePropertyModifications(outfitScaleAdjuster);
            }
        }

        internal static void MatchScaleAdjusters(Transform armatureRoot)
        {
            if (armatureRoot == null) return;

            foreach (var mergeArmature in armatureRoot
                         .GetComponentsInChildren<ModularAvatarMergeArmature>(true)
                         .Where(HasValidTarget))
            {
                MatchScaleAdjusters(mergeArmature);
            }
        }

        internal static void ForcePositionToBaseAvatar(
            Transform armatureRoot,
            MergeArmaturePositionResetOptions options)
        {
            if (armatureRoot == null) return;

            var mergeArmatures = armatureRoot
                .GetComponentsInChildren<ModularAvatarMergeArmature>(true)
                .Where(HasValidTarget)
                .ToArray();
            var primaryMergeArmature = armatureRoot.GetComponent<ModularAvatarMergeArmature>();
            if (!HasValidTarget(primaryMergeArmature))
            {
                primaryMergeArmature = mergeArmatures.FirstOrDefault();
            }

            foreach (var mergeArmature in mergeArmatures)
            {
                ForcePositionToBaseAvatar(mergeArmature, options,
                    mergeArmature != primaryMergeArmature);
            }
        }
    }
}