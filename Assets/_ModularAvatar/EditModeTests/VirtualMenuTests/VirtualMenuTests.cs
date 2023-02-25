using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor.menu;
using nadena.dev.modular_avatar.core.menu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace modular_avatar_tests.VirtualMenuTests
{
    public class VirtualMenuTests : TestBase
    {
        private Texture2D testTex;
        private List<UnityEngine.Object> toDestroy;
        private int controlIndex;

        public override void Setup()
        {
            base.Setup();
            testTex = new Texture2D(1, 1);
            toDestroy = new List<UnityEngine.Object>();
            controlIndex = 0;
        }

        public override void Teardown()
        {
            base.Teardown();
            Object.DestroyImmediate(testTex);
            foreach (var obj in toDestroy)
            {
                Object.DestroyImmediate(obj);
            }
        }

        private T Create<T>(string name = null) where T : ScriptableObject
        {
            if (name == null) name = GUID.Generate().ToString();

            T val = ScriptableObject.CreateInstance<T>();
            val.name = name;

            toDestroy.Add(val);

            return val;
        }

        [Test]
        public void TestEmptyMenu()
        {
            var virtualMenu = new VirtualMenu(null);
            virtualMenu.FreezeMenu();

            Assert.AreEqual(1, virtualMenu.ResolvedMenu.Count);
            var root = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(0, root.Controls.Count);
            Assert.AreSame(RootMenu.Instance, root.NodeKey);
        }

        [Test]
        public void TestBasicMenu()
        {
            var rootMenu = Create<VRCExpressionsMenu>();

            rootMenu.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl(),
                GenerateTestControl()
            };

            var virtualMenu = new VirtualMenu(rootMenu);
            virtualMenu.FreezeMenu();

            Assert.AreEqual(1, virtualMenu.ResolvedMenu.Count);
            var root = virtualMenu.ResolvedMenu[rootMenu];
            Assert.AreEqual(2, root.Controls.Count);
            Assert.AreSame(rootMenu, root.NodeKey);
            AssertControlEquals(rootMenu.controls[0], root.Controls[0]);
            AssertControlEquals(rootMenu.controls[1], root.Controls[1]);
        }

        [Test]
        public void TestNativeMenuWithCycles()
        {
            var rootMenu = Create<VRCExpressionsMenu>("root");
            var sub1 = Create<VRCExpressionsMenu>("sub1");
            var sub2 = Create<VRCExpressionsMenu>("sub2");

            rootMenu.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestSubmenu(sub1)
            };

            sub1.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestSubmenu(sub2)
            };

            sub2.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestSubmenu(rootMenu)
            };

            var virtualMenu = new VirtualMenu(rootMenu);
            virtualMenu.FreezeMenu();

            Assert.AreEqual(3, virtualMenu.ResolvedMenu.Count);
            var rootNode = virtualMenu.ResolvedMenu[rootMenu];
            var sub1Node = virtualMenu.ResolvedMenu[sub1];
            var sub2Node = virtualMenu.ResolvedMenu[sub2];

            Assert.AreEqual(1, rootNode.Controls.Count);
            Assert.AreSame(rootMenu, rootNode.NodeKey);
            Assert.AreSame(sub1Node, rootNode.Controls[0].SubmenuNode);
            Assert.IsNull(rootNode.Controls[0].subMenu);

            Assert.AreEqual(1, sub1Node.Controls.Count);
            Assert.AreSame(sub1, sub1Node.NodeKey);
            Assert.AreSame(sub2Node, sub1Node.Controls[0].SubmenuNode);
            Assert.IsNull(sub1Node.Controls[0].subMenu);

            Assert.AreEqual(1, sub2Node.Controls.Count);
            Assert.AreSame(sub2, sub2Node.NodeKey);
            Assert.AreSame(rootNode, sub2Node.Controls[0].SubmenuNode);
            Assert.IsNull(sub2Node.Controls[0].subMenu);
        }

        [Test]
        public void TestBasicMenuInstaller()
        {
            VRCExpressionsMenu testMenu = Create<VRCExpressionsMenu>();
            testMenu.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl(),
                GenerateTestControl()
            };

            var installer = CreateInstaller("test");
            installer.menuToAppend = testMenu;

            var virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer);
            virtualMenu.FreezeMenu();

            Assert.AreEqual(1, virtualMenu.ResolvedMenu.Count);
            var root = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(2, root.Controls.Count);
            Assert.AreSame(RootMenu.Instance, root.NodeKey);
            AssertControlEquals(testMenu.controls[0], root.Controls[0]);
            AssertControlEquals(testMenu.controls[1], root.Controls[1]);
        }

        [Test]
        public void TestMenuItemInstaller()
        {
            var installer = CreateInstaller("test");
            installer.menuToAppend = Create<VRCExpressionsMenu>();

            var item = installer.gameObject.AddComponent<ModularAvatarMenuItem>();
            item.Control = GenerateTestControl();

            var virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer);
            virtualMenu.FreezeMenu();

            item.Control.name = "test";

            Assert.AreEqual(1, virtualMenu.ResolvedMenu.Count);
            var root = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(1, root.Controls.Count);
            Assert.AreSame(RootMenu.Instance, root.NodeKey);
            AssertControlEquals(item.Control, root.Controls[0]);
        }

        [Test]
        public void TestInstallOntoInstaller()
        {
            var installer_a = CreateInstaller("a");
            var installer_b = CreateInstaller("b");
            var installer_c = CreateInstaller("c");

            var menu_a = Create<VRCExpressionsMenu>();
            var menu_b = Create<VRCExpressionsMenu>();
            var menu_c = Create<VRCExpressionsMenu>();

            menu_a.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };
            menu_b.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };
            menu_c.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };

            installer_a.menuToAppend = menu_a;
            installer_b.menuToAppend = menu_b;
            installer_c.menuToAppend = menu_c;

            installer_c.installTargetMenu = installer_a.menuToAppend;

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);
            virtualMenu.RegisterMenuInstaller(installer_b);
            virtualMenu.RegisterMenuInstaller(installer_c);

            virtualMenu.FreezeMenu();
            Assert.AreEqual(1, virtualMenu.ResolvedMenu.Count);
            var rootMenu = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(3, rootMenu.Controls.Count);
            Assert.AreSame(RootMenu.Instance, rootMenu.NodeKey);
            AssertControlEquals(menu_a.controls[0], rootMenu.Controls[0]);
            AssertControlEquals(menu_c.controls[0], rootMenu.Controls[1]);
            AssertControlEquals(menu_b.controls[0], rootMenu.Controls[2]);
        }

        [Test]
        public void TestInstallSubmenu()
        {
            var installer_a = CreateInstaller("a");

            var menu_a = Create<VRCExpressionsMenu>("a");
            var menu_b = Create<VRCExpressionsMenu>("b");
            menu_a.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestSubmenu(menu_b)
            };
            menu_b.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };

            installer_a.menuToAppend = menu_a;

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);

            virtualMenu.FreezeMenu();

            Assert.AreEqual(2, virtualMenu.ResolvedMenu.Count);
            var rootMenu = virtualMenu.ResolvedMenu[RootMenu.Instance];
            var subMenu = virtualMenu.ResolvedMenu[menu_b];
            Assert.AreSame(subMenu, rootMenu.Controls[0].SubmenuNode);
            Assert.AreSame(RootMenu.Instance, rootMenu.NodeKey);
            Assert.AreSame(menu_b, subMenu.NodeKey);
            Assert.AreEqual(1, subMenu.Controls.Count);
            AssertControlEquals(menu_b.controls[0], subMenu.Controls[0]);
        }

        [Test]
        public void TestYankInstaller()
        {
            var installer_a = CreateInstaller("a");
            var installer_b = CreateInstaller("b");

            var menu_a = Create<VRCExpressionsMenu>("a");
            menu_a.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };
            installer_a.menuToAppend = menu_a;

            var item = installer_b.gameObject.AddComponent<ModularAvatarMenuItem>();
            item.Control = GenerateTestControl();
            item.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            item.MenuSource = SubmenuSource.Children;

            var child = CreateChild(item.gameObject, "child");
            var childItem = child.AddComponent<ModularAvatarMenuInstallTarget>();
            childItem.installer = installer_a;

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);
            virtualMenu.RegisterMenuInstaller(installer_b);
            virtualMenu.RegisterMenuInstallTarget(childItem);

            virtualMenu.FreezeMenu();
            item.Control.name = "b";

            Assert.AreEqual(2, virtualMenu.ResolvedMenu.Count);
            var rootMenu = virtualMenu.ResolvedMenu[RootMenu.Instance];
            var item_node = virtualMenu.ResolvedMenu[new MenuNodesUnder(item.gameObject)];
            Assert.AreEqual(1, rootMenu.Controls.Count);
            Assert.AreSame(RootMenu.Instance, rootMenu.NodeKey);
            AssertControlEquals(item.Control, rootMenu.Controls[0]);
            Assert.AreSame(item_node, rootMenu.Controls[0].SubmenuNode);

            Assert.AreEqual(1, item_node.Controls.Count);
            Assert.AreEqual(new MenuNodesUnder(item.gameObject), item_node.NodeKey);
            AssertControlEquals(menu_a.controls[0], item_node.Controls[0]);
        }

        [Test]
        public void WhenMenuInstallersLoop_LoopIsTerminated()
        {
            var installer_a = CreateInstaller("a");
            var installer_b = CreateInstaller("b");

            var menu_a = Create<VRCExpressionsMenu>("a");
            menu_a.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };
            installer_a.menuToAppend = menu_a;

            var menu_b = Create<VRCExpressionsMenu>("b");
            menu_b.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };
            installer_b.menuToAppend = menu_b;

            installer_a.installTargetMenu = menu_b;
            installer_b.installTargetMenu = menu_a;

            VirtualMenu virtualMenu = new VirtualMenu(menu_a);
            virtualMenu.RegisterMenuInstaller(installer_a);
            virtualMenu.RegisterMenuInstaller(installer_b);

            virtualMenu.FreezeMenu();

            Assert.AreEqual(1, virtualMenu.ResolvedMenu.Count);
            var rootMenu = virtualMenu.ResolvedMenu[menu_a];
            var menu_a_node = virtualMenu.ResolvedMenu[menu_a];
            Assert.AreEqual(3, rootMenu.Controls.Count);
        }

        [Test]
        public void TestExternalSubmenuSource_WithMenuInstaller()
        {
            var installer_a = CreateInstaller("a");
            var installer_b = CreateInstaller("b");

            var menu_a = Create<VRCExpressionsMenu>("a");
            menu_a.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };

            var menu_b = Create<VRCExpressionsMenu>("b");
            menu_b.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };

            var item_a = installer_a.gameObject.AddComponent<ModularAvatarMenuItem>();
            item_a.Control = GenerateTestControl();
            item_a.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            item_a.MenuSource = SubmenuSource.MenuAsset;
            item_a.Control.subMenu = menu_a;

            installer_b.menuToAppend = menu_b;
            installer_b.installTargetMenu = menu_a;

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);
            virtualMenu.RegisterMenuInstaller(installer_b);

            virtualMenu.FreezeMenu();

            var rootNode = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(VRCExpressionsMenu.Control.ControlType.SubMenu, rootNode.Controls[0].type);
            var menu_a_node = rootNode.Controls[0].SubmenuNode;
            AssertControlEquals(menu_a.controls[0], menu_a_node.Controls[0]);
            AssertControlEquals(menu_b.controls[0], menu_a_node.Controls[1]);
        }

        [Test]
        public void MenuItem_NestedSubmenuNodes()
        {
            var installer_a = CreateInstaller("root");
            var root_item = installer_a.gameObject.AddComponent<ModularAvatarMenuItem>();
            root_item.Control = GenerateTestControl();
            root_item.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            root_item.MenuSource = SubmenuSource.Children;

            var mid_obj = CreateChild(root_item.gameObject, "mid");
            var mid_item = mid_obj.AddComponent<ModularAvatarMenuItem>();
            mid_item.Control = GenerateTestControl();
            mid_item.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            mid_item.MenuSource = SubmenuSource.Children;

            var leaf_obj = CreateChild(mid_obj, "leaf");
            var leaf_item = leaf_obj.AddComponent<ModularAvatarMenuItem>();
            leaf_item.Control = GenerateTestControl();

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);

            virtualMenu.FreezeMenu();

            var rootNode = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(VRCExpressionsMenu.Control.ControlType.SubMenu, rootNode.Controls[0].type);
            var mid_node = rootNode.Controls[0].SubmenuNode;
            var leaf_node = mid_node.Controls[0].SubmenuNode;

            Assert.AreEqual(1, rootNode.Controls.Count);
            Assert.AreEqual(1, mid_node.Controls.Count);
            Assert.AreEqual(1, leaf_node.Controls.Count);

            root_item.Control.name = "root";
            mid_item.Control.name = "mid";
            leaf_item.Control.name = "leaf";

            AssertControlEquals(root_item.Control, rootNode.Controls[0]);
            AssertControlEquals(mid_item.Control, mid_node.Controls[0]);
            AssertControlEquals(leaf_item.Control, leaf_node.Controls[0]);
        }

        [Test]
        public void MenuItem_RemoteReference()
        {
            var installer_a = CreateInstaller("root");
            var root_item = installer_a.gameObject.AddComponent<ModularAvatarMenuItem>();

            var extern_root = CreateRoot("test");
            var extern_obj = CreateChild(extern_root, "control");
            var extern_item = extern_obj.AddComponent<ModularAvatarMenuItem>();
            extern_item.Control = GenerateTestControl();

            root_item.Control = GenerateTestControl();
            root_item.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            root_item.MenuSource = SubmenuSource.Children;
            root_item.menuSource_otherObjectChildren = extern_root;

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);

            virtualMenu.FreezeMenu();

            root_item.Control.name = "root";
            extern_item.Control.name = "control";

            var rootNode = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(VRCExpressionsMenu.Control.ControlType.SubMenu, rootNode.Controls[0].type);
            var extern_node = rootNode.Controls[0].SubmenuNode;

            Assert.AreEqual(1, rootNode.Controls.Count);
            Assert.AreEqual(1, extern_node.Controls.Count);

            AssertControlEquals(root_item.Control, rootNode.Controls[0]);
            AssertControlEquals(extern_item.Control, extern_node.Controls[0]);
        }

        [Test]
        public void InstallerInstallsInstallTarget()
        {
            var installer_a = CreateInstaller("a");
            var installer_b = CreateInstaller("b");

            var menu_b = Create<VRCExpressionsMenu>("b");
            menu_b.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl()
            };
            installer_b.menuToAppend = menu_b;

            var item_a = installer_a.gameObject.AddComponent<ModularAvatarMenuInstallTarget>();
            item_a.installer = installer_b;

            VirtualMenu virtualMenu = new VirtualMenu(null);
            virtualMenu.RegisterMenuInstaller(installer_a);
            virtualMenu.RegisterMenuInstaller(installer_b);
            virtualMenu.RegisterMenuInstallTarget(item_a);

            virtualMenu.FreezeMenu();

            var rootNode = virtualMenu.ResolvedMenu[RootMenu.Instance];
            Assert.AreEqual(1, rootNode.Controls.Count);
            AssertControlEquals(menu_b.controls[0], rootNode.Controls[0]);
        }

        [Test]
        public void TestSerializeMenu()
        {
            var menu_a = Create<VRCExpressionsMenu>("test");
            var menu_b = Create<VRCExpressionsMenu>("test2");
            var menu_c = Create<VRCExpressionsMenu>("test3");

            menu_a.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl(),
                GenerateTestSubmenu(menu_b),
            };

            menu_b.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestControl(),
                GenerateTestSubmenu(menu_c),
            };

            menu_c.controls = new List<VRCExpressionsMenu.Control>()
            {
                GenerateTestSubmenu(menu_a),
            };

            var virtualMenu = new VirtualMenu(menu_a);
            virtualMenu.FreezeMenu();

            var assetSet = new HashSet<UnityEngine.Object>();
            var serialized = virtualMenu.SerializeMenu(obj => assetSet.Add(obj));

            Assert.AreEqual(3, assetSet.Count);
            Assert.AreEqual(2, serialized.controls.Count);

            AssertControlEquals(menu_a.controls[0], serialized.controls[0]);
            AssertControlEquals(menu_a.controls[1], serialized.controls[1]);

            var serialized_b = serialized.controls[1].subMenu;
            Assert.AreEqual(2, serialized_b.controls.Count);

            AssertControlEquals(menu_b.controls[0], serialized_b.controls[0]);
            AssertControlEquals(menu_b.controls[1], serialized_b.controls[1]);

            var serialized_c = serialized_b.controls[1].subMenu;
            Assert.AreEqual(1, serialized_c.controls.Count);

            AssertControlEquals(menu_c.controls[0], serialized_c.controls[0]);

            Assert.True(assetSet.Contains(serialized));
            Assert.True(assetSet.Contains(serialized_b));
            Assert.True(assetSet.Contains(serialized_c));
        }


        ModularAvatarMenuInstaller CreateInstaller(string name)
        {
            GameObject obj = new GameObject();
            obj.name = name;

            var installer = obj.AddComponent<ModularAvatarMenuInstaller>();
            installer.name = name;

            toDestroy.Add(obj);

            return installer;
        }

        VRCExpressionsMenu.Control GenerateTestSubmenu(VRCExpressionsMenu menu)
        {
            var control = GenerateTestControl();
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            control.subMenu = menu;

            return control;
        }

        VRCExpressionsMenu.Control GenerateTestControl()
        {
            var control = new VRCExpressionsMenu.Control();

            VRCExpressionsMenu.Control.ControlType[] types = new[]
            {
                VRCExpressionsMenu.Control.ControlType.Button,
                // VRCExpressionsMenu.Control.ControlType.SubMenu,
                VRCExpressionsMenu.Control.ControlType.Toggle,
                VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
            };

            control.type = types[Random.Range(0, types.Length)];
            control.name = "Test Control " + controlIndex++;
            control.parameter = new VRCExpressionsMenu.Control.Parameter();
            control.parameter.name = "Test Parameter " + GUID.Generate();
            control.icon = new Texture2D(1, 1);
            control.labels = new[]
            {
                new VRCExpressionsMenu.Control.Label()
                {
                    name = "label",
                    icon = testTex
                }
            };
            control.subParameters = new[]
            {
                new VRCExpressionsMenu.Control.Parameter()
                {
                    name = "Test Sub Parameter " + GUID.Generate()
                }
            };
            control.value = 0.42f;
            control.style = VRCExpressionsMenu.Control.Style.Style3;

            return control;
        }

        void AssertControlEquals(VRCExpressionsMenu.Control expected, VRCExpressionsMenu.Control actual)
        {
            Assert.AreEqual(expected.type, actual.type);
            Assert.AreEqual(expected.name, actual.name);
            Assert.AreEqual(expected.parameter.name, actual.parameter.name);
            Assert.AreNotSame(expected.parameter, actual.parameter);
            Assert.AreEqual(expected.icon, actual.icon);
            Assert.AreEqual(expected.labels.Length, actual.labels.Length);
            Assert.AreEqual(expected.labels[0].name, actual.labels[0].name);
            Assert.AreEqual(expected.labels[0].icon, actual.labels[0].icon);
            Assert.AreEqual(expected.subParameters.Length, actual.subParameters.Length);
            Assert.AreEqual(expected.subParameters[0].name, actual.subParameters[0].name);
            Assert.AreNotSame(expected.subParameters[0], actual.subParameters[0]);
            Assert.AreEqual(expected.value, actual.value);
            Assert.AreEqual(expected.style, actual.style);
        }
    }
}