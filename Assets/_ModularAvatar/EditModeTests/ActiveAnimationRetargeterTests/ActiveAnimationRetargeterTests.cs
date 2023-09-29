using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class ActiveAnimationRetargeterTests : TestBase
{
    [Test]
    public void SimpleRetarget() {
        var avatar = CreatePrefab("SimpleRetarget.prefab");
        var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();

        // initialize context
        var buildContext = new BuildContext(descriptor);
        var pathMappings = buildContext.PluginBuildContext.ActivateExtensionContext<TrackObjectRenamesContext>();
        new MergeAnimatorProcessor().OnPreprocessAvatar(avatar, buildContext); // we need this for AnimationDatabase
        buildContext.PluginBuildContext.ActivateExtensionContext<AnimationDatabase>();

        // get game objects
        var changedChild = avatar.transform.Find("Toggled/Child");
        var newParent = avatar.transform.Find("NewParent");

        // do retargeting
        var retargeter = new ActiveAnimationRetargeter(buildContext, new BoneDatabase(), changedChild);
        var created = retargeter.CreateIntermediateObjects(newParent.gameObject);
        retargeter.FixupAnimations();

        // commit
        buildContext.PluginBuildContext.DeactivateExtensionContext<AnimationDatabase>();

        var clip = findFxClip(avatar, layerName: "retarget");
        var curveBindings = AnimationUtility.GetCurveBindings(clip);

        // Intermediate object must be created
        Assert.That(created, Is.Not.EqualTo(newParent.gameObject));

        // The created animation must have m_IsActive of intermediate object
        Assert.That(curveBindings, Does.Contain(EditorCurveBinding.FloatCurve(
            pathMappings.GetObjectIdentifier(created), typeof(GameObject), "m_IsActive")));
    }
}