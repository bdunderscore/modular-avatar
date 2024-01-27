#if MA_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

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

        [Test]
        public void TestParameterConflicts()
        {
            var prefab = CreatePrefab("ParameterConflicts.prefab");

            var context = CreateContext(prefab);
            var maContext = context.ActivateExtensionContext<ModularAvatarContext>().BuildContext;
            
            var errors = ErrorReport.CaptureErrors(
                () =>
                {
                    using (new ObjectRegistryScope(new ObjectRegistry(prefab.transform)))
                    {
                        new RenameParametersHook().OnPreprocessAvatar(prefab, maContext);
                    }
                });

            var valueConflict = errors
                .Select(e => e.TheError)
                .Cast<SimpleError>()
                .First(e => e.TitleKey == "error.rename_params.default_value_conflict");
            Assert.AreEqual("a$$Internal_1", valueConflict.DetailsSubst[0]);
            Assert.AreEqual("0", valueConflict.DetailsSubst[1]);
            Assert.AreEqual("1", valueConflict.DetailsSubst[2]);
            
            Assert.AreEqual("Conflict/P", valueConflict.References[0].Path);
            Assert.AreEqual("Conflict/P (1)", valueConflict.References[1].Path);
            
            var typeConflict = errors
                .Select(e => e.TheError)
                .Cast<SimpleError>()
                .First(e => e.TitleKey == "error.rename_params.type_conflict");
            
            Assert.AreEqual("a$$Internal_2", typeConflict.DetailsSubst[0]);
            Assert.AreEqual("Int", typeConflict.DetailsSubst[1]);
            Assert.AreEqual("Float", typeConflict.DetailsSubst[2]);
            
            Assert.AreEqual("TypeConflict/P", typeConflict.References[0].Path);
            Assert.AreEqual("TypeConflict/P (2)", typeConflict.References[1].Path);
        }

        [Test]
        public void TestParameterResults()
        {
            var prefab = CreatePrefab("ParameterResolution.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var expParams = prefab.GetComponent<VRCAvatarDescriptor>().expressionParameters;
            
            Assert.AreEqual(expParams.parameters[0].name, "a");
            Assert.IsTrue(Mathf.Abs(expParams.parameters[0].defaultValue - 0.1f) < 0.0001f);
        }
    }
}

#endif