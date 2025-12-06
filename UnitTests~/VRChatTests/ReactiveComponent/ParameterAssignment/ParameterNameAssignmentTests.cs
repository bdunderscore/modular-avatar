using System.Linq;
using modular_avatar_tests;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class ParameterNameAssignmentTests : TestBase
{
    [Test]
    public void MenuItemsWithChildRC_CreateParameterOnAnimator()
    {
        var prefab = CreatePrefab("MenuItemsWithChildRC_CreateParameterOnAnimator.prefab");

        AvatarProcessor.ProcessAvatar(prefab);

        var fx = (AnimatorController)FindFxController(prefab).animatorController;
        var expMenu = prefab.GetComponent<VRCAvatarDescriptor>().expressionsMenu;

        var toggleParam = expMenu.controls.Find(c => c.name == "toggle").parameter.name;
        Assert.IsTrue(fx.parameters.Any(p => p.name == toggleParam && p.type == AnimatorControllerParameterType.Float));
    }
}
