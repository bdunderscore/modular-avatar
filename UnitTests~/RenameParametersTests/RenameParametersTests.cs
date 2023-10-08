using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace modular_avatar_tests.RenameParametersTests
{
    public class RenameParametersTests : TestBase
    {
        [Test]
        public void RenameParametersRenamesParamDrivers()
        {
            var prefab = CreatePrefab("RenameParametersRenamesParamDrivers.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var layer = findFxLayer(prefab, "test");
            CheckDriver(layer.stateMachine.behaviours[0] as VRCAvatarParameterDriver);

            var state = FindStateInLayer(layer, "s");
            CheckDriver(state.behaviours[0] as VRCAvatarParameterDriver);

            var subStateMachine = layer.stateMachine.stateMachines[0].stateMachine;
            CheckDriver(subStateMachine.behaviours[0] as VRCAvatarParameterDriver);

            var subState = FindStateInLayer(layer, "s");
            CheckDriver(subState.behaviours[0] as VRCAvatarParameterDriver);

            void CheckDriver(VRCAvatarParameterDriver driver)
            {
                Assert.AreEqual("param_new", driver.parameters[0].name);
            }
        }

        [Test]
        public void RenameInstalledMenu()
        {
            var prefab = CreatePrefab("RenameInstalledMenu.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var menu = prefab.GetComponent<VRCAvatarDescriptor>().expressionsMenu;
            Assert.AreEqual("test$$Internal_0", menu.controls[0].parameter.name);
            Assert.AreEqual("test$$Internal_0", menu.controls[1].subMenu.controls[0].parameter.name);
        }

        [Test]
        public void TestRecursiveMenu()
        {
            var menu = LoadAsset<VRCExpressionsMenu>("m1.asset");
            var avatar = CreateRoot("root");
            var child = CreateChild(avatar, "child");
            var param = child.AddComponent<ModularAvatarParameters>();
            param.parameters.Add(new ParameterConfig()
            {
                nameOrPrefix = "test",
                internalParameter = true,
            });
            var installer = child.AddComponent<ModularAvatarMenuInstaller>();
            installer.menuToAppend = menu;

            AvatarProcessor.ProcessAvatar(avatar);
        }
    }
}