using System.Collections;
using System.Diagnostics.CodeAnalysis;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTests._PlayModeTests.ReactiveComponents
{
    [SuppressMessage("ReSharper", "Unity.PreferAddressByIdToGraphicsParams")]
    public class ObjectToggleTests : TestBase
    {
        [UnityTest]
        public IEnumerator TestBasicObjectToggleBehavior()
        {
            var avatar = CreateRoot();
            var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prim.transform.parent = avatar.transform;
            
            var toggle = CreateChild(avatar, "toggle");
            var mi = toggle.AddComponent<ModularAvatarMenuItem>();
            var ot = toggle.AddComponent<ModularAvatarObjectToggle>();

            mi.PortableControl.Parameter = "abc";
            ot.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(prim),
                Active = false
            });
            
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            
            yield return null;
            Assert.IsTrue(prim.activeSelf);
            
            SetParam(animator, "abc", 1);
            yield return null;
            Assert.IsFalse(prim.activeSelf);
        }

        [UnityTest]
        public IEnumerator TestMultipleObjectToggles_DifferentGameObjects_TraversalOrder()
        {
            var avatar = CreateRoot();
            var prim = CreatePrim(avatar, "Cube");

            // First toggle gameobject (earlier in traversal): sets prim inactive when its param is ON
            var toggleA = CreateChild(avatar, "toggleA");
            var miA = toggleA.AddComponent<ModularAvatarMenuItem>();
            var otA = toggleA.AddComponent<ModularAvatarObjectToggle>();
            miA.PortableControl.Parameter = "pA";
            otA.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(prim),
                Active = false
            });

            // Second toggle gameobject (later in traversal): sets prim active when its param is ON
            var toggleB = CreateChild(avatar, "toggleB");
            var miB = toggleB.AddComponent<ModularAvatarMenuItem>();
            var otB = toggleB.AddComponent<ModularAvatarObjectToggle>();
            miB.PortableControl.Parameter = "pB";
            otB.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(prim),
                Active = true
            });

            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);

            yield return null;
            Assert.IsTrue(prim.activeSelf);

            // Turn both toggles ON: last in traversal (toggleB) should win and prim should be active
            SetParam(animator, "pA", 1);
            SetParam(animator, "pB", 1);
            yield return null;
            Assert.IsTrue(prim.activeSelf);

            // If only the earlier one is ON, prim should be inactive
            SetParam(animator, "pB", 0);
            yield return null;
            Assert.IsFalse(prim.activeSelf);
        }

        [UnityTest]
        public IEnumerator TestMultipleObjectToggles_SameGameObject_LastComponentWins()
        {
            var avatar = CreateRoot();
            var prim = CreatePrim(avatar, "Cube");
            prim.SetActive(false);

            var toggleGO = CreateChild(avatar, "toggleSame");

            // Add first menu+toggle component pair (earlier component)
            var mi1 = toggleGO.AddComponent<ModularAvatarMenuItem>();
            var ot1 = toggleGO.AddComponent<ModularAvatarObjectToggle>();
            mi1.PortableControl.Parameter = "s1";
            ot1.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(prim),
                Active = false
            });

            ot1.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(prim),
                Active = true
            });

            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);

            yield return null;
            Assert.IsFalse(prim.activeSelf);
            
            SetParam(animator, "s1", 1);
            yield return null;
            Assert.IsTrue(prim.activeSelf);
        }

        [UnityTest]
        public IEnumerator TestObjectToggle_TargetsAnotherToggle_ANDGateWithMenu()
        {
            var avatar = CreateRoot();
            var prim = CreatePrim(avatar, "Cube");

            // Target toggle (toggleB) -- has a menu and an object toggle that affects prim
            var toggleB = CreateChild(avatar, "toggleB");
            var mb = toggleB.AddComponent<ModularAvatarMenuItem>();
            var otB = toggleB.AddComponent<ModularAvatarObjectToggle>();
            mb.PortableControl.Parameter = "pb";
            otB.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(prim),
                Active = false
            });

            // Controller toggle (toggleA) -- targets the toggleB GameObject itself
            var toggleA = CreateChild(avatar, "toggleA");
            var ma = toggleA.AddComponent<ModularAvatarMenuItem>();
            var otA = toggleA.AddComponent<ModularAvatarObjectToggle>();
            ma.PortableControl.Parameter = "pa";
            otA.Objects.Add(new ToggledObject()
            {
                Object = new AvatarObjectReference(toggleB),
                // When pa == 1 we set toggleB inactive, which should prevent otB from applying.
                Active = false
            });

            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);
            yield return null;
            
            Assert.IsTrue(prim.activeSelf);

            // If pb is ON and pa is OFF, otB can apply and should set prim inactive
            SetParam(animator, "pb", 1);
            SetParam(animator, "pa", 0);
            yield return null;
            Assert.IsFalse(prim.activeSelf);

            // If both pb and pa are ON, otA will deactivate toggleB, preventing otB from applying.
            SetParam(animator, "pa", 1);
            SetParam(animator, "pb", 1);
            // There is a one-frame delay currently
            yield return null;
            yield return null;
            // Because toggleB is deactivated by otA, otB should not run; prim remains active.
            Assert.IsTrue(prim.activeSelf);

            // Clean-up case: if pa goes OFF again while pb stays ON, otB should apply again
            SetParam(animator, "pa", 0);
            yield return null;
            yield return null;
            Assert.IsFalse(prim.activeSelf);
        }

        [UnityTest]
        public IEnumerator OnBeforeOff()
        {
            var avatar = CreateRoot();
            var p1 = CreatePrim(avatar, "p1");
            var p2 = CreatePrim(avatar, "p2");
            
            var p1ot = p1.AddComponent<ModularAvatarObjectToggle>();
            p1ot.Objects = new()
            {
                new()
                {
                    Object = new(p2),
                    Active = true
                }
            };
            p1ot.Inverted = true;

            var menu = CreateChild(avatar, "menu");
            var mi = menu.AddComponent<ModularAvatarMenuItem>();
            var ot = menu.AddComponent<ModularAvatarObjectToggle>();
            mi.PortableControl.Parameter = "p";
            ot.Objects = new()
            {
                new()
                {
                    Object = new(p1),
                    Active = false
                }
            };
            
            ProcessAvatar(avatar);
            var animator = ActivateFX(avatar);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Assert.IsTrue(p1.activeSelf);
            }
            
            // When we toggle OFF p1, we'll toggle ON p2. At no time should both be disabled.
            // Currently, we do allow for time when both might be enabled.
            SetParam(animator, "p", 1);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
                Assert.IsTrue(p1.activeSelf || p2.activeSelf);
                if (!p1.activeSelf && p2.activeSelf)
                {
                    break;
                }
            }
            
            Assert.AreEqual(false, p1.activeSelf);
            Assert.AreEqual(true, p2.activeSelf);
        }
    }
}