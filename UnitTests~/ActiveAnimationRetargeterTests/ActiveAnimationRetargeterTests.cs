#if MA_VRCSDK3_AVATARS

using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using EditorCurveBinding = UnityEditor.EditorCurveBinding;

public class ActiveAnimationRetargeterTests : TestBase
{
    [Test]
    public void SimpleRetarget()
    {
        var avatar = CreatePrefab("SimpleRetarget.prefab");

        // initialize context
        var buildContext = new BuildContext(avatar);
        var pathMappings = buildContext.PluginBuildContext.ActivateExtensionContext<AnimationServicesContext>()
            .PathMappings;

        // get game objects
        var changedChild = avatar.transform.Find("Toggled/Child");
        var newParent = avatar.transform.Find("NewParent");

        // do retargeting
        var retargeter = new ActiveAnimationRetargeter(buildContext, new BoneDatabase(), changedChild);
        var created = retargeter.CreateIntermediateObjects(newParent.gameObject);
        retargeter.FixupAnimations();

        // commit
        buildContext.AnimationDatabase.Commit();

        var clip = findFxClip(avatar, layerName: "retarget");
        var curveBindings = AnimationUtility.GetCurveBindings(clip);

        // Intermediate object must be created
        Assert.That(created, Is.Not.EqualTo(newParent.gameObject));

        // The created animation must have m_IsActive of intermediate object
        Assert.That(curveBindings, Does.Contain(EditorCurveBinding.FloatCurve(
            pathMappings.GetObjectIdentifier(created), typeof(GameObject), "m_IsActive")));
    }
}

#endif