#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using nadena.dev.ndmf;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPass : Pass<ScaleAdjusterPass>
    {
        private static readonly FieldInfo? SkeletonBoneParent = AccessTools.Field(typeof(SkeletonBone), "parentName");
        
        protected override void Execute(ndmf.BuildContext context)
        {
            HashSet<Transform> adjustedBones = new(); 
            Dictionary<Transform, Transform> boneMappings = new Dictionary<Transform, Transform>();

            foreach (var adjuster in context.AvatarRootObject.GetComponentsInChildren<ModularAvatarScaleAdjuster>(true))
            {
                adjustedBones.Add(adjuster.transform);
                
                var proxyObject = new GameObject("ScaleProxy");
                var proxyTransform = proxyObject.transform;
                proxyObject.AddComponent<ModularAvatarPBBlocker>();

                proxyTransform.SetParent(adjuster.transform, false);
                proxyTransform.localPosition = Vector3.zero;
                proxyTransform.localRotation = Quaternion.identity;
                proxyTransform.localScale = adjuster.Scale;

                boneMappings.Add(adjuster.transform, proxyTransform);

                Object.DestroyImmediate(adjuster);
            }
            
            if (boneMappings.Count == 0)
            {
                return;
            }

            foreach (var smr in context.AvatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && boneMappings.TryGetValue(bones[i], out var newBone))
                    {
                        bones[i] = newBone;
                    }
                }
                smr.bones = bones;
            }
            
            // Correct the humanoid avatar descriptor for any bones we might have moved (that is - any children of
            // Scale Adjusters).

            var mappings = BuildBoneMapping(context, out var avatar);
            if (avatar == null) return;

            var humanDescription = avatar.humanDescription;
            
            var humanProxy = BuildHumanProxy(context.AvatarRootTransform, mappings);
            if (humanProxy == null || humanDescription.skeleton.Length == 0)
            {
                Debug.Log("[ScaleAdjusterPass] No humanoid bones were recognized");
                return;
            }

            try
            {
                foreach (var hbm in mappings)
                {
                    if (hbm.Bone == null || !adjustedBones.Contains(hbm.Bone)) continue;

                    foreach (var child in hbm.Children)
                    {
                        if (child.Bone == null) continue;
                        humanDescription.skeleton[child.BoneIndex].position = child.Bone.localPosition;
                    }
                }

                var newAvatar = AvatarBuilder.BuildHumanAvatar(humanProxy, humanDescription);
                if (!newAvatar.isValid)
                {
                    Debug.LogWarning("[ScaleAdjusterPass] Avatar is not valid");
                    return;
                }

                context.AssetSaver.SaveAsset(newAvatar);
                context.AvatarRootObject.GetComponent<Animator>().avatar = newAvatar;
            }
            finally
            {
                Object.DestroyImmediate(humanProxy);
            }
        }

        GameObject? BuildHumanProxy(Transform root, List<HumanBoneMapping> mappings)
        {
            HashSet<Transform> humanBodyBones = mappings
                .Where(m => m.HumanBoneEnum != null && m.Bone != null)
                .Select(m => m.Bone!)
                .ToHashSet();

            return CloneObject(root, root);

            GameObject? CloneObject(Transform parent, Transform original)
            {
                if (!humanBodyBones.Any(hbb => hbb == original || hbb.IsChildOf(original)))
                {
                    return null;
                }

                var obj = new GameObject(original.gameObject.name);
                var xform = obj.transform;
                xform.SetParent(parent);
                xform.SetLocalPositionAndRotation(original.localPosition, original.localRotation);
                xform.localScale = original.localScale;

                foreach (Transform child in original)
                {
                    CloneObject(xform, child);
                }

                return obj;
            }
        }


        class HumanBoneMapping
        {
            public HumanBodyBones? HumanBoneEnum;
            public Transform? Bone;
            public int BoneIndex;

            public List<HumanBoneMapping> Children = new();
        }
        
        // Phase 1 - identify human bones
        // Phase 2 - update child bone positions based on scale

        List<HumanBoneMapping> BuildBoneMapping(ndmf.BuildContext context, out Avatar? avatar)
        {
            avatar = null;

            if (SkeletonBoneParent == null) return new();
            
            var animator = context.AvatarRootObject?.GetComponent<Animator>();
            if (animator == null) return new();

            if (animator.avatar?.humanDescription == null) return new();

            avatar = animator.avatar;

            var humanDesc = avatar.humanDescription;

            Dictionary<HumanBodyBones, HumanBoneMapping> humanBones = new();
            Dictionary<string, HumanBoneMapping> byName = new();
            List<HumanBoneMapping> allBones = new();

            // The Mechanim skeleton is a bit... fuzzy. Each bone references its parent _by name_, and we have to infer
            // the hierarchy based on whether that bone actually exists. What's more, the same bone name may be reused;
            // we perform fuzzy matching here based on assuming that the closer index wins (ie assuming things should be
            // in hierarchy order).
            
            var skeletonBones = humanDesc.skeleton;
            for (int i = 0; i < skeletonBones.Length; i++)
            {
                var bone = skeletonBones[i];
                var mapping = new HumanBoneMapping()
                {
                    BoneIndex = i
                };

                if (i == 0)
                {
                    mapping.Bone = context.AvatarRootTransform;
                }
                else
                {
                    var parentName = SkeletonBoneParent.GetValue(bone) as string;
                    if (!string.IsNullOrWhiteSpace(parentName) && byName.TryGetValue(parentName, out var parentMapping))
                    {
                        parentMapping.Children.Add(mapping);
                        mapping.Bone = parentMapping.Bone?.Find(bone.name);
                    }
                }

                // Overwrite the mapping as we go, so subsequent bones will bind to the earlier ones
                byName[bone.name] = mapping;
                allBones.Add(mapping);
            }

            foreach (var humanBone in humanDesc.human)
            {
                var boneName = humanBone.boneName;
                var humanName = humanBone.humanName.Replace(" ", "");
                if (!Enum.TryParse<HumanBodyBones>(humanName, out var boneEnum))
                {
                    Debug.LogWarning($"Failed to parse Mechanim bone name: {humanName}");
                    continue;
                }

                if (!byName.TryGetValue(boneName, out var mapping))
                {
                    Debug.LogWarning($"Failed to find mapped bone: {boneName}");
                    continue;
                }

                mapping.HumanBoneEnum = boneEnum;
                humanBones[boneEnum] = mapping;
            }

            return allBones;
        }
    }
}