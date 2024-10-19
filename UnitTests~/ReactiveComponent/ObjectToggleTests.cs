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
        public void WhenObjectIsAlwaysOn_CorrectProxyParameterIsGenerated()
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

            // TODO: Ideally we should start using play mode testing for these things...
            var fx = (AnimatorController)FindFxController(root).animatorController;
            var readableProp = fx.parameters.FirstOrDefault(
                p => p.name.StartsWith("__MA/ReadableProp/obj/UnityEngine.GameObject/m_IsActive")
            );
            
            Assert.IsNotNull(readableProp);
            Assert.AreEqual(readableProp.defaultFloat, 0);
            
            Assert.IsFalse(obj.activeSelf);
        }
    }
}