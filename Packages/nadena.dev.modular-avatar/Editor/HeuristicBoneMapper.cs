using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class HeuristicBoneMapper
    {
        // This list is originally from https://github.com/HhotateA/AvatarModifyTools/blob/d8ae75fed8577707253d6b63a64d6053eebbe78b/Assets/HhotateA/AvatarModifyTool/Editor/EnvironmentVariable.cs#L81-L139
        // Copyright (c) 2021 @HhotateA_xR
        // Licensed under the MIT License
        private static string[][] boneNamePatterns = new[]
        {
            new[] {"Hips", "Hip"},
            new[] {"LeftUpperLeg", "UpperLeg_Left", "UpperLeg_L", "Leg_Left", "Leg_L", "ULeg_L"},
            new[] {"RightUpperLeg", "UpperLeg_Right", "UpperLeg_R", "Leg_Right", "Leg_R", "ULeg_R"},
            new[] {"LeftLowerLeg", "LowerLeg_Left", "LowerLeg_L", "Knee_Left", "Knee_L", "LLeg_L"},
            new[] {"RightLowerLeg", "LowerLeg_Right", "LowerLeg_R", "Knee_Right", "Knee_R", "LLeg_R"},
            new[] {"LeftFoot", "Foot_Left", "Foot_L"},
            new[] {"RightFoot", "Foot_Right", "Foot_R"},
            new[] {"Spine"},
            new[] {"Chest"},
            new[] {"Neck"},
            new[] {"Head"},
            new[] {"LeftShoulder", "Shoulder_Left", "Shoulder_L"},
            new[] {"RightShoulder", "Shoulder_Right", "Shoulder_R"},
            new[] {"LeftUpperArm", "UpperArm_Left", "UpperArm_L", "Arm_Left", "Arm_L", "UArm_L"},
            new[] {"RightUpperArm", "UpperArm_Right", "UpperArm_R", "Arm_Right", "Arm_R", "UArm_R"},
            new[] {"LeftLowerArm", "LowerArm_Left", "LowerArm_L", "LArm_L"},
            new[] {"RightLowerArm", "LowerArm_Right", "LowerArm_R", "LArm_R"},
            new[] {"LeftHand", "Hand_Left", "Hand_L"},
            new[] {"RightHand", "Hand_Right", "Hand_R"},
            new[] {"LeftToes", "Toes_Left", "Toe_Left", "ToeIK_L", "Toes_L", "Toe_L"},
            new[] {"RightToes", "Toes_Right", "Toe_Right", "ToeIK_R", "Toes_R", "Toe_R"},
            new[] {"LeftEye", "Eye_Left", "Eye_L"},
            new[] {"RightEye", "Eye_Right", "Eye_R"},
            new[] {"Jaw"},
            new[] {"LeftThumbProximal", "ProximalThumb_Left", "ProximalThumb_L", "Thumb1_L"},
            new[] {"LeftThumbIntermediate", "IntermediateThumb_Left", "IntermediateThumb_L", "Thumb2_L"},
            new[] {"LeftThumbDistal", "DistalThumb_Left", "DistalThumb_L", "Thumb3_L"},
            new[] {"LeftIndexProximal", "ProximalIndex_Left", "ProximalIndex_L", "Index1_L"},
            new[] {"LeftIndexIntermediate", "IntermediateIndex_Left", "IntermediateIndex_L", "Index2_L"},
            new[] {"LeftIndexDistal", "DistalIndex_Left", "DistalIndex_L", "Index3_L"},
            new[] {"LeftMiddleProximal", "ProximalMiddle_Left", "ProximalMiddle_L", "Middle1_L"},
            new[] {"LeftMiddleIntermediate", "IntermediateMiddle_Left", "IntermediateMiddle_L", "Middle2_L"},
            new[] {"LeftMiddleDistal", "DistalMiddle_Left", "DistalMiddle_L", "Middle3_L"},
            new[] {"LeftRingProximal", "ProximalRing_Left", "ProximalRing_L", "Ring1_L"},
            new[] {"LeftRingIntermediate", "IntermediateRing_Left", "IntermediateRing_L", "Ring2_L"},
            new[] {"LeftRingDistal", "DistalRing_Left", "DistalRing_L", "Ring3_L"},
            new[] {"LeftLittleProximal", "ProximalLittle_Left", "ProximalLittle_L", "Little1_L"},
            new[] {"LeftLittleIntermediate", "IntermediateLittle_Left", "IntermediateLittle_L", "Little2_L"},
            new[] {"LeftLittleDistal", "DistalLittle_Left", "DistalLittle_L", "Little3_L"},
            new[] {"RightThumbProximal", "ProximalThumb_Right", "ProximalThumb_R", "Thumb1_R"},
            new[] {"RightThumbIntermediate", "IntermediateThumb_Right", "IntermediateThumb_R", "Thumb2_R"},
            new[] {"RightThumbDistal", "DistalThumb_Right", "DistalThumb_R", "Thumb3_R"},
            new[] {"RightIndexProximal", "ProximalIndex_Right", "ProximalIndex_R", "Index1_R"},
            new[] {"RightIndexIntermediate", "IntermediateIndex_Right", "IntermediateIndex_R", "Index2_R"},
            new[] {"RightIndexDistal", "DistalIndex_Right", "DistalIndex_R", "Index3_R"},
            new[] {"RightMiddleProximal", "ProximalMiddle_Right", "ProximalMiddle_R", "Middle1_R"},
            new[] {"RightMiddleIntermediate", "IntermediateMiddle_Right", "IntermediateMiddle_R", "Middle2_R"},
            new[] {"RightMiddleDistal", "DistalMiddle_Right", "DistalMiddle_R", "Middle3_R"},
            new[] {"RightRingProximal", "ProximalRing_Right", "ProximalRing_R", "Ring1_R"},
            new[] {"RightRingIntermediate", "IntermediateRing_Right", "IntermediateRing_R", "Ring2_R"},
            new[] {"RightRingDistal", "DistalRing_Right", "DistalRing_R", "Ring3_R"},
            new[] {"RightLittleProximal", "ProximalLittle_Right", "ProximalLittle_R", "Little1_R"},
            new[] {"RightLittleIntermediate", "IntermediateLittle_Right", "IntermediateLittle_R", "Little2_R"},
            new[] {"RightLittleDistal", "DistalLittle_Right", "DistalLittle_R", "Little3_R"},
            new[] {"UpperChest", "UChest"},
        };

        internal static string NormalizeName(string name)
        {
            return name.ToLowerInvariant()
                .Replace("_", "")
                .Replace(".", "")
                .Replace(" ", "");
        }

        internal static readonly ImmutableDictionary<string, HumanBodyBones> NameToBoneMap;
        internal static readonly ImmutableDictionary<HumanBodyBones, ImmutableList<string>> BoneToNameMap;

        static HeuristicBoneMapper()
        {
            var nameToBoneMap = new Dictionary<string, HumanBodyBones>();
            var boneToNameMap = new Dictionary<HumanBodyBones, ImmutableList<string>>();

            for (int i = 0; i < boneNamePatterns.Length; i++)
            {
                var bone = (HumanBodyBones) i;
                foreach (var name in boneNamePatterns[i])
                {
                    RegisterNameForBone(NormalizeName(name), bone);
                }
            }

            void RegisterNameForBone(string name, HumanBodyBones bone)
            {
                nameToBoneMap[name] = bone;
                if (!boneToNameMap.TryGetValue(bone, out var names))
                {
                    names = ImmutableList<string>.Empty;
                }

                if (!names.Contains(name))
                {
                    boneToNameMap[bone] = names.Add(name);
                }
            }

            NameToBoneMap = nameToBoneMap.ToImmutableDictionary();
            BoneToNameMap = boneToNameMap.ToImmutableDictionary();
        }


        /// <summary>
        /// Examines the children of src, and tries to map them to the corresponding child of newParent.
        /// Unmappable bones will not be added to the resulting dictionary. Ensures that each parent bone is only mapped
        /// once.
        /// </summary>
        internal static Dictionary<Transform, Transform> AssignBoneMappings(
            ModularAvatarMergeArmature config,
            GameObject src,
            GameObject newParent
        )
        {
            HashSet<Transform> unassigned = new HashSet<Transform>();
            Dictionary<Transform, Transform> mappings = new Dictionary<Transform, Transform>();
            List<Transform> heuristicAssignmentPass = new List<Transform>();

            foreach (Transform child in newParent.transform)
            {
                unassigned.Add(child);
            }

            foreach (Transform child in src.transform)
            {
                var childName = child.gameObject.name;
                if (childName.StartsWith(config.prefix) && childName.EndsWith(config.suffix))
                {
                    var targetObjectName = childName.Substring(config.prefix.Length,
                        childName.Length - config.prefix.Length - config.suffix.Length);
                    var targetObject = newParent.transform.Find(targetObjectName);

                    if (targetObject != null && unassigned.Contains(targetObject))
                    {
                        mappings[child] = targetObject;
                        unassigned.Remove(targetObject);
                    }
                    else
                    {
                        heuristicAssignmentPass.Add(child);
                    }
                }
            }

            Dictionary<string, Transform> lcNameToXform = new Dictionary<string, Transform>();
            foreach (var target in unassigned)
            {
                lcNameToXform[NormalizeName(target.gameObject.name)] = target;
            }

            foreach (var child in heuristicAssignmentPass)
            {
                var childName = child.gameObject.name;
                var targetObjectName = childName.Substring(config.prefix.Length,
                    childName.Length - config.prefix.Length - config.suffix.Length);

                if (!NameToBoneMap.TryGetValue(
                        NormalizeName(targetObjectName.ToLowerInvariant()), out var bodyBone))
                {
                    continue;
                }

                foreach (var otherName in BoneToNameMap[bodyBone])
                {
                    if (lcNameToXform.TryGetValue(otherName, out var targetObject))
                    {
                        mappings[child] = targetObject;
                        unassigned.Remove(targetObject);
                        lcNameToXform.Remove(otherName.ToLowerInvariant());
                        break;
                    }
                }
            }

            return mappings;
        }

        internal static void RenameBonesByHeuristic(ModularAvatarMergeArmature config)
        {
            var target = config.mergeTarget.Get(RuntimeUtil.FindAvatarInParents(config.transform));
            if (target == null) return;

            Traverse(config.transform, target.transform);

            void Traverse(Transform src, Transform dst)
            {
                var mappings = AssignBoneMappings(config, src.gameObject, dst.gameObject);

                foreach (var pair in mappings)
                {
                    var newName = config.prefix + pair.Value.gameObject.name + config.suffix;
                    var srcGameObj = pair.Key.gameObject;
                    var oldName = srcGameObj.name;

                    if (oldName != newName)
                    {
                        Undo.RecordObject(srcGameObj, "Applying heuristic mapping");
                        srcGameObj.name = newName;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(srcGameObj);
                    }

                    Traverse(pair.Key, pair.Value);
                }
            }
        }
    }
}