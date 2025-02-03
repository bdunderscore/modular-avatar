#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using EditorCurveBinding = UnityEditor.EditorCurveBinding;

public class ActiveAnimationRetargeterTests : TestBase
{
    [Test]
    public void SimpleRetarget()
    {
        var avatar = CreatePrefab("SimpleRetarget.prefab");

        // initialize context
        var buildContext = new BuildContext(avatar);
        var asc = buildContext.PluginBuildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();

        // get game objects
        var changedChild = avatar.transform.Find("Toggled/Child");
        var newParent = avatar.transform.Find("NewParent");

        // do retargeting
        var retargeter = new ActiveAnimationRetargeter(buildContext, new BoneDatabase(), changedChild);
        var created = retargeter.CreateIntermediateObjects(newParent.gameObject);
        retargeter.FixupAnimations();

        var fx = asc.ControllerContext[VRCAvatarDescriptor.AnimLayerType.FX]!;
        var clip = (VirtualClip) fx.Layers.First(l => l.Name == "retarget").StateMachine.DefaultState!.Motion;
        var curveBindings = clip!.GetFloatCurveBindings();

        // Intermediate object must be created
        Assert.That(created, Is.Not.EqualTo(newParent.gameObject));

        // The created animation must have m_IsActive of intermediate object
        Assert.That(curveBindings, Does.Contain(EditorCurveBinding.FloatCurve(
            asc.ObjectPathRemapper.GetVirtualPathForObject(created), typeof(GameObject), "m_IsActive")));
    }
}

#endif