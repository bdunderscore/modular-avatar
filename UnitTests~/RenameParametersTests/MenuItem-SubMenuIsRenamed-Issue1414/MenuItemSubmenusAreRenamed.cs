#if MA_VRCSDK3_AVATARS

using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using VRC.SDK3.Avatars.Components;

public class MenuItemSubmenusAreRenamed : TestBase
{
    [Test]
    public void TestMenuItemSubmenusAreRenamed()
    {
        var root = CreatePrefab("Issue1414.prefab");
        
        AvatarProcessor.ProcessAvatar(root);

        var menu = root.GetComponent<VRCAvatarDescriptor>().expressionsMenu;
        var submenu = menu.controls[0].subMenu;
        var button = submenu.controls[0];
        
        Assert.AreNotEqual(button.parameter.name, "testparameter");
    }
}

#endif