namespace nadena.dev.modular_avatar.core.editor
{
    internal class BoneNameMappings
    {
        // This list is originally from https://github.com/HhotateA/AvatarModifyTools/blob/d8ae75fed8577707253d6b63a64d6053eebbe78b/Assets/HhotateA/AvatarModifyTool/Editor/EnvironmentVariable.cs#L81-L139
        // Copyright (c) 2021 @HhotateA_xR
        // Licensed under the MIT License
        public static string[][] boneNamePatterns = new string[][]
        {
            new string[] {"Hips", "Hip"},
            new string[] {"LeftUpperLeg", "UpperLeg_Left", "UpperLeg_L", "Leg_Left", "Leg_L"},
            new string[] {"RightUpperLeg", "UpperLeg_Right", "UpperLeg_R", "Leg_Right", "Leg_R"},
            new string[] {"LeftLowerLeg", "LowerLeg_Left", "LowerLeg_L", "Knee_Left", "Knee_L"},
            new string[] {"RightLowerLeg", "LowerLeg_Right", "LowerLeg_R", "Knee_Right", "Knee_R"},
            new string[] {"LeftFoot", "Foot_Left", "Foot_L"},
            new string[] {"RightFoot", "Foot_Right", "Foot_R"},
            new string[] {"Spine"},
            new string[] {"Chest"},
            new string[] {"Neck"},
            new string[] {"Head"},
            new string[] {"LeftShoulder", "Shoulder_Left", "Shoulder_L"},
            new string[] {"RightShoulder", "Shoulder_Right", "Shoulder_R"},
            new string[] {"LeftUpperArm", "UpperArm_Left", "UpperArm_L", "Arm_Left", "Arm_L"},
            new string[] {"RightUpperArm", "UpperArm_Right", "UpperArm_R", "Arm_Right", "Arm_R"},
            new string[] {"LeftLowerArm", "LowerArm_Left", "LowerArm_L"},
            new string[] {"RightLowerArm", "LowerArm_Right", "LowerArm_R"},
            new string[] {"LeftHand", "Hand_Left", "Hand_L"},
            new string[] {"RightHand", "Hand_Right", "Hand_R"},
            new string[] {"LeftToes", "Toes_Left", "Toe_Left", "ToeIK_L", "Toes_L", "Toe_L"},
            new string[] {"RightToes", "Toes_Right", "Toe_Right", "ToeIK_R", "Toes_R", "Toe_R"},
            new string[] {"LeftEye", "Eye_Left", "Eye_L"},
            new string[] {"RightEye", "Eye_Right", "Eye_R"},
            new string[] {"Jaw"},
            new string[] {"LeftThumbProximal", "ProximalThumb_Left", "ProximalThumb_L"},
            new string[] {"LeftThumbIntermediate", "IntermediateThumb_Left", "IntermediateThumb_L"},
            new string[] {"LeftThumbDistal", "DistalThumb_Left", "DistalThumb_L"},
            new string[] {"LeftIndexProximal", "ProximalIndex_Left", "ProximalIndex_L"},
            new string[] {"LeftIndexIntermediate", "IntermediateIndex_Left", "IntermediateIndex_L"},
            new string[] {"LeftIndexDistal", "DistalIndex_Left", "DistalIndex_L"},
            new string[] {"LeftMiddleProximal", "ProximalMiddle_Left", "ProximalMiddle_L"},
            new string[] {"LeftMiddleIntermediate", "IntermediateMiddle_Left", "IntermediateMiddle_L"},
            new string[] {"LeftMiddleDistal", "DistalMiddle_Left", "DistalMiddle_L"},
            new string[] {"LeftRingProximal", "ProximalRing_Left", "ProximalRing_L"},
            new string[] {"LeftRingIntermediate", "IntermediateRing_Left", "IntermediateRing_L"},
            new string[] {"LeftRingDistal", "DistalRing_Left", "DistalRing_L"},
            new string[] {"LeftLittleProximal", "ProximalLittle_Left", "ProximalLittle_L"},
            new string[] {"LeftLittleIntermediate", "IntermediateLittle_Left", "IntermediateLittle_L"},
            new string[] {"LeftLittleDistal", "DistalLittle_Left", "DistalLittle_L"},
            new string[] {"RightThumbProximal", "ProximalThumb_Right", "ProximalThumb_R"},
            new string[] {"RightThumbIntermediate", "IntermediateThumb_Right", "IntermediateThumb_R"},
            new string[] {"RightThumbDistal", "DistalThumb_Right", "DistalThumb_R"},
            new string[] {"RightIndexProximal", "ProximalIndex_Right", "ProximalIndex_R"},
            new string[] {"RightIndexIntermediate", "IntermediateIndex_Right", "IntermediateIndex_R"},
            new string[] {"RightIndexDistal", "DistalIndex_Right", "DistalIndex_R"},
            new string[] {"RightMiddleProximal", "ProximalMiddle_Right", "ProximalMiddle_R"},
            new string[] {"RightMiddleIntermediate", "IntermediateMiddle_Right", "IntermediateMiddle_R"},
            new string[] {"RightMiddleDistal", "DistalMiddle_Right", "DistalMiddle_R"},
            new string[] {"RightRingProximal", "ProximalRing_Right", "ProximalRing_R"},
            new string[] {"RightRingIntermediate", "IntermediateRing_Right", "IntermediateRing_R"},
            new string[] {"RightRingDistal", "DistalRing_Right", "DistalRing_R"},
            new string[] {"RightLittleProximal", "ProximalLittle_Right", "ProximalLittle_R"},
            new string[] {"RightLittleIntermediate", "IntermediateLittle_Right", "IntermediateLittle_R"},
            new string[] {"RightLittleDistal", "DistalLittle_Right", "DistalLittle_R"},
            new string[] {"UpperChest"},
            new string[] {"LastBone", "Armature"}, // 本来的ではないけど，Rootもhitさせたい
        };
    }
}