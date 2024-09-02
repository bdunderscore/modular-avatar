using System.Reflection;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

public class PreferFirstHipsMatch : TestBase
{
    [Test]
    public void SetupHeuristicPrefersFirstHipsMatch()
    {
        var root = CreateCommonPrefab("shapell.fbx");
#if MA_VRCSDK3_AVATARS
        root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
        var root_hips = root.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).gameObject;
        var root_armature = root_hips.transform.parent.gameObject;
        var root_secondary = CreateChild(root, "PBC");
        var root_alt_hips = CreateChild(root_secondary, "Hips");

        var outfit = CreateChild(root, "Outfit");
        var outfit_armature = CreateChild(outfit, "Armature");
        var outfit_hips = CreateChild(outfit_armature, "Hips");
        
        Assert.IsTrue(SetupOutfit.FindBones(outfit, out var det_av_root, out var det_av_hips, out var det_outfit_hips));
        Assert.AreSame(root, det_av_root);
        Assert.AreSame(root_hips, det_av_hips);
        Assert.AreSame(outfit_hips, det_outfit_hips);
    }
}
