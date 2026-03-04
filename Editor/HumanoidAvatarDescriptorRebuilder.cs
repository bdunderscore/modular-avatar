#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using nadena.dev.modular_avatar.core.editor.plugin;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class HumanoidAvatarDescriptorRebuilder
    {
        private static readonly FieldInfo? SkeletonBoneParent = AccessTools.Field(typeof(SkeletonBone), "parentName");

        private readonly ndmf.BuildContext _context;
        private readonly Action<HumanBoneMapping, HumanDescription> _updateBonePositions;

        private HumanoidAvatarDescriptorRebuilder(ndmf.BuildContext context,
            Action<HumanBoneMapping, HumanDescription> updateBonePositions)
        {
            _context = context;
            _updateBonePositions = updateBonePositions;
        }

        public static void Rebuild(ndmf.BuildContext context,
            Action<HumanBoneMapping, HumanDescription> updateBonePositions)
        {
            var descriptor = new HumanoidAvatarDescriptorRebuilder(context, updateBonePositions);
            descriptor.Rebuild();
        }

        public void Rebuild()
        {
            var mappings = BuildBoneMapping(out var avatar);
            if (avatar == null) return;

            var humanDescription = avatar.humanDescription;

            var humanProxy = BuildHumanProxy(_context.AvatarRootTransform, mappings);
            if (humanProxy == null || humanDescription.skeleton.Length == 0)
            {
                Debug.Log("[HumanoidAvatarDescriptorRebuilder] No humanoid bones were recognized");
                return;
            }

            try
            {
                foreach (var hbm in mappings)
                {
                    if (hbm.Bone == null) continue;
                    _updateBonePositions(hbm, humanDescription);
                }

                var newAvatar = AvatarBuilder.BuildHumanAvatar(humanProxy, humanDescription);
                if (!newAvatar.isValid)
                {
                    Debug.LogWarning("[HumanoidAvatarDescriptorRebuilder] Avatar is not valid");
                    return;
                }

                _context.AssetSaver.SaveAsset(newAvatar);
                RebindHumanoidAvatar.RebindAvatar(_context.AvatarRootObject.GetComponent<Animator>(), newAvatar);
            }
            finally
            {
                Object.DestroyImmediate(humanProxy);
            }
        }

        private GameObject? BuildHumanProxy(Transform root, List<HumanBoneMapping> mappings)
        {
            var humanBodyBones = mappings
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

        private List<HumanBoneMapping> BuildBoneMapping(out Avatar? avatar)
        {
            avatar = null;

            if (SkeletonBoneParent == null) return new List<HumanBoneMapping>();

            var animator = _context.AvatarRootObject?.GetComponent<Animator>();
            if (animator == null) return new List<HumanBoneMapping>();

            if (animator.avatar?.humanDescription == null) return new List<HumanBoneMapping>();

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
            for (var i = 0; i < skeletonBones.Length; i++)
            {
                var bone = skeletonBones[i];
                var mapping = new HumanBoneMapping
                {
                    BoneIndex = i
                };

                if (i == 0)
                {
                    mapping.Bone = _context.AvatarRootTransform;
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

        internal class HumanBoneMapping
        {
            public HumanBodyBones? HumanBoneEnum;
            public Transform? Bone;
            public int BoneIndex;

            public List<HumanBoneMapping> Children = new();
        }
    }
}