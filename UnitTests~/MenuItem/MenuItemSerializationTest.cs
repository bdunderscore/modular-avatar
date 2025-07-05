#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

public class MenuItemSerializationTest : TestBase
{
    [Test]
    public void ValidateMenuItemSerialization()
    {
        var prefab = CreatePrefab("MenuItemSerializationTest.prefab");
        
        var toggle = prefab.transform.Find("Toggle").GetComponent<ModularAvatarMenuItem>().PortableControl;
        Assert.AreEqual(PortableControlType.Toggle, toggle.Type);
        Assert.AreEqual("p_toggle", toggle.Parameter);
        Assert.AreEqual(1f, toggle.Value);
        Assert.AreEqual(null, toggle.VRChatSubMenu);
        Assert.AreEqual(0, toggle.SubParameters.Count);
        Assert.AreEqual(0, toggle.Labels.Count);
        
        var button = prefab.transform.Find("Button").GetComponent<ModularAvatarMenuItem>().PortableControl;
        Assert.AreEqual(PortableControlType.Button, button.Type);
        Assert.AreEqual("p_button", button.Parameter);
        Assert.AreEqual(2f, button.Value);
        Assert.AreEqual(null, button.VRChatSubMenu);
        Assert.AreEqual(0, button.SubParameters.Count);
        Assert.AreEqual(0, button.Labels.Count);
        
        var subMenu = prefab.transform.Find("Submenu").GetComponent<ModularAvatarMenuItem>().PortableControl;
        Assert.AreEqual(PortableControlType.SubMenu, subMenu.Type);
        Assert.AreEqual("p_submenu", subMenu.Parameter);
        Assert.AreEqual(3f, subMenu.Value);
        // TODO: submenu?
        Assert.AreEqual(0, subMenu.SubParameters.Count);
        Assert.AreEqual(0, subMenu.Labels.Count);
        
        var twoAxis = prefab.transform.Find("2axis-puppet").GetComponent<ModularAvatarMenuItem>().PortableControl;
        Assert.AreEqual(PortableControlType.TwoAxisPuppet, twoAxis.Type);
        Assert.AreEqual("p_2", twoAxis.Parameter);
        Assert.AreEqual(4f, twoAxis.Value);
        Assert.AreEqual("up", twoAxis.Labels[0].name);
        Assert.NotNull(twoAxis.Labels[0].icon);
        Assert.AreEqual("right", twoAxis.Labels[1].name);
        Assert.NotNull(twoAxis.Labels[1].icon);
        Assert.AreEqual("down", twoAxis.Labels[2].name);
        Assert.NotNull(twoAxis.Labels[2].icon);
        Assert.AreEqual("left", twoAxis.Labels[3].name);
        Assert.NotNull(twoAxis.Labels[3].icon);
        Assert.That(twoAxis.SubParameters, Is.EquivalentTo(new [] { "x", "y" }));
        // TODO: fix labels for this case
        
        var fourAxis = prefab.transform.Find("4axis-puppet").GetComponent<ModularAvatarMenuItem>().PortableControl;
        Assert.AreEqual(PortableControlType.FourAxisPuppet, fourAxis.Type);
        Assert.AreEqual("p_4", fourAxis.Parameter);
        Assert.AreEqual(5f, fourAxis.Value);
        Assert.That(fourAxis.SubParameters, Is.EquivalentTo(new [] { "up", "right", "down", "left" }));
        Assert.AreEqual("up", fourAxis.Labels[0].name);
        Assert.NotNull(fourAxis.Labels[0].icon);
        Assert.AreEqual("right", fourAxis.Labels[1].name);
        Assert.NotNull(fourAxis.Labels[1].icon);
        Assert.AreEqual("down", fourAxis.Labels[2].name);
        Assert.NotNull(fourAxis.Labels[2].icon);
        Assert.AreEqual("left", fourAxis.Labels[3].name);
        Assert.NotNull(fourAxis.Labels[3].icon);
        
        var radial = prefab.transform.Find("radial-puppet").GetComponent<ModularAvatarMenuItem>().PortableControl;
        Assert.AreEqual(PortableControlType.RadialPuppet, radial.Type);
        Assert.AreEqual("p_r", radial.Parameter);
        Assert.AreEqual(6f, radial.Value);
        Assert.That(radial.SubParameters, Is.EquivalentTo(new [] { "radial" }));
    }
  
[Test]
    public void SetPortableControlFields_UpdatesValuesCorrectly()
    {
        var root = CreateRoot("root");
        var go = new GameObject("MenuItem");
        go.transform.parent = root.transform;
        var menuItem = go.AddComponent<ModularAvatarMenuItem>();
        var control = menuItem.PortableControl;

        UnityEngine.Object? subMenu = null;
        #if MA_VRCSDK3_AVATARS
        subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        #endif
        
        control.Type = PortableControlType.Button;
        control.Parameter = "test_param";
        control.Value = 42f;
        control.VRChatSubMenu = subMenu;
        control.SubParameters = (new [] { "a", "b" }).ToImmutableList();
        control.Labels = (new[]
        {
            new PortableLabel { Name = "label1", Icon = new Texture2D(2, 2) },
            new PortableLabel { Name = "label2", Icon = new Texture2D(2, 2) }
        }).ToImmutableList();

        var result = menuItem.PortableControl;
        Assert.AreEqual(PortableControlType.Button, result.Type);
        Assert.AreEqual("test_param", result.Parameter);
        Assert.AreEqual(42f, result.Value);
        Assert.AreEqual(subMenu, result.VRChatSubMenu);
        Assert.That(result.SubParameters, Is.EquivalentTo(new[] { "a", "b" }));
        Assert.AreEqual(2, result.Labels.Count);
        Assert.AreEqual("label1", result.Labels[0].name);
        Assert.NotNull(result.Labels[0].icon);
        Assert.AreEqual("label2", result.Labels[1].name);
        Assert.NotNull(result.Labels[1].icon);
    }}
