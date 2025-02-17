#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.ReactiveComponent
{
    internal class ObjectToggleTests : TestBase
    {
        [Test]
        public void WhenObjectIsAlwaysOn_CorrectObjectStateIsSelected()
        {
            var root = CreateRoot("root");
            var obj = CreateChild(root, "obj");
            var toggle = CreateChild(root, "toggle");
            
            // Prevent obj from being removed by the GC game objects pass
            obj.AddComponent<MeshRenderer>();
            
            var toggleComponent = toggle.AddComponent<ModularAvatarObjectToggle>();
            var aor = new AvatarObjectReference();
            aor.Set(obj);
            
            toggleComponent.Objects = new()
            {
                new()
                {
                    Active = false,
                    Object = aor
                }
            };
            
            AvatarProcessor.ProcessAvatar(root);
            
            Assert.IsFalse(obj.activeSelf);
        }
    }
}

#endif