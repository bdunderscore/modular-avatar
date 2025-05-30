﻿using System.Collections.Generic;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ModularAvatarMergeArmature = nadena.dev.modular_avatar.core.ModularAvatarMergeArmature;

#if MA_VRCSDK3_AVATARS
using System;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

public class MergeArmatureTests : TestBase
{
    private const string SHAPELL_FBX_GUID = "1418fcab2d94f9d4982cd714b598900f";
    
    [Test]
    public void DontStripObjectsWithComponents()
    {
        var root = CreatePrefab("ColliderMergeTest.prefab");

        AvatarProcessor.ProcessAvatar(root);

        // We expect two children:  Mergable and Hips$[uuid] (retained due to the BoxCollider)
        var targetHips = root.transform.Find("TargetArmature/Hips");
        Assert.AreEqual(2, targetHips.childCount);
        Assert.AreEqual("Mergable", targetHips.GetChild(0).gameObject.name);

        Assert.NotNull(targetHips.GetChild(1).GetComponent<BoxCollider>());
    }

#if MA_VRCSDK3_AVATARS

    [Test]
    public void DontMergePartiallySamePhysBoneChain()
    {
        var root = CreatePrefab("PartiallySamePhysBoneChain.prefab");
        var physBone = root.transform.Find("GameObject/PhysBone").GetComponent<VRCPhysBone>();
        var physBoneTarget = root.transform.Find("GameObject/Armature (1)/L_1");

        AvatarProcessor.ProcessAvatar(root);

        var targetHips = root.transform.Find("Armature");
        
        Assert.AreEqual(1, targetHips.childCount);
        Assert.AreEqual("L_1", targetHips.GetChild(0).gameObject.name);
        var l1 = targetHips.GetChild(0).transform;
        var l1x = l1.GetChild(l1.childCount - 1).transform;
        Assert.That(l1x.gameObject.name, Does.StartWith("L_1$"));

        Assert.That(l1x, Is.EqualTo(physBoneTarget));
        Assert.That(physBone.ignoreTransforms, Is.Empty);
    }

    [Test]
    public void PhysBonesNRETest()
    {
        var root = LoadShapell();
        var avdesc = root.AddComponent<VRCAvatarDescriptor>();
        avdesc.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];
        avdesc.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];

        var outfit1 = LoadShapell();
        outfit1.transform.SetParent(root.transform, false);
        
        var outfit2 = LoadShapell();
        outfit2.transform.SetParent(root.transform, false);
        
        var outfit1_hips = outfit1.transform.Find("Armature/Hips");
        var outfit2_hips = outfit2.transform.Find("Armature/Hips");
        var root_hips = root.transform.Find("Armature/Hips");
        
        root_hips.gameObject.AddComponent<VRCPhysBone>();
        outfit1_hips.gameObject.AddComponent<VRCPhysBone>();
        outfit2_hips.gameObject.AddComponent<VRCPhysBone>();
        
        var root_smr = root.transform.Find("Body").gameObject.GetComponent<SkinnedMeshRenderer>();
        var outfit1_smr = outfit1.transform.Find("Body").gameObject.GetComponent<SkinnedMeshRenderer>();
        var outfit2_smr = outfit2.transform.Find("Body").gameObject.GetComponent<SkinnedMeshRenderer>();
        
        outfit1.transform.Find("Armature").gameObject.AddComponent<ModularAvatarMergeArmature>().mergeTarget.Set(
            root.transform.Find("Armature").gameObject
        );
        
        outfit2.transform.Find("Armature").gameObject.AddComponent<ModularAvatarMergeArmature>().mergeTarget.Set(
            root.transform.Find("Armature").gameObject
        );

        AvatarProcessor.ProcessAvatar(root);

        foreach (var (root_bone, outfit_bone) in Enumerable.Zip(root_smr.bones, outfit1_smr.bones, (x, y) => (x, y)))
        {
            Assert.AreSame(root_bone, outfit_bone);
        }
        
        foreach (var (root_bone, outfit_bone) in Enumerable.Zip(root_smr.bones, outfit2_smr.bones, (x, y) => (x, y)))
        {
            Assert.AreSame(root_bone, outfit_bone);
        }
    }

    [Test]
    public void RetainsTransformLookthroughTest()
    {
        var root = CreatePrefab("TransformLookthrough/RetainsTransformLookthroughCurves.prefab");

        AvatarProcessor.ProcessAvatar(root);

        var testLayer = findFxLayer(root, "test");
        var motion = (AnimationClip)testLayer.stateMachine.defaultState.motion;

        var subArmature = root.transform.GetChild(0).GetChild(0).GetChild(0);
        var armaturePath = RuntimeUtil.AvatarRootPath(subArmature.gameObject);
        var hipsPath = "Armature/Hips";
            
        var curves = AnimationUtility.GetCurveBindings(motion)
            .Select(ecb => (ecb.path,  ecb.type, ecb.propertyName))
            .ToHashSet();
        var expectedCurves = new HashSet<(string, Type, string)>()
        {
            (armaturePath, typeof(GameObject), "m_IsActive"),
            (hipsPath, typeof(Transform), "m_LocalScale.x"),
            (hipsPath, typeof(Transform), "m_LocalScale.y"),
            (hipsPath, typeof(Transform), "m_LocalScale.z"),
            // It's not clear if we should be animating the root Armature object here, but this matches the pre-1.12
            // behavior and is therefore probably best from a compatibility perspective
            ("Armature", typeof(GameObject), "m_IsActive")
        };
            
        Assert.That(expectedCurves, Is.EquivalentTo(curves));
    }
#endif

    private static GameObject LoadShapell()
    {
        return GameObject.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(SHAPELL_FBX_GUID)
            )
        );
    }
}