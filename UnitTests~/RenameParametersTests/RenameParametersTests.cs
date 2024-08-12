#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
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

        [Test]
        public void AnimatorOnlyParametersTests()
        {
            var prefab = CreatePrefab("AnimatorOnlyParameterValues/AOPV.prefab");

            AvatarProcessor.ProcessAvatar(prefab);

            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController as AnimatorController;
            Assert.NotNull(fx);

            AssertParamStates(fx);

            var action = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController as AnimatorController;
            Assert.NotNull(action);

            AssertParamStates(action);

            void AssertParamStates(AnimatorController controller)
            {
                var parameters = controller.parameters.Select(
                    p => new KeyValuePair<String, AnimatorControllerParameter>(p.name, p)
                ).ToImmutableDictionary();

                Assert.LessOrEqual(Mathf.Abs(parameters["float"].defaultFloat - 0.5f), 0.005f);
                Assert.AreEqual(23, parameters["int"].defaultInt);
                Assert.AreEqual(true, parameters["bool"].defaultBool);
            }
        }

        [Test]
        public void SavedParameterResolution()
        {
            var prefab = CreatePrefab("SavedParamResolution.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var expParams = prefab.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters
                .Select(p => new KeyValuePair<string, VRCExpressionParameters.Parameter>(p.name, p))
                .ToImmutableDictionary();
            
            Assert.IsTrue(expParams["a"].saved);
            Assert.IsTrue(expParams["b"].saved);
            Assert.IsFalse(expParams["c"].saved);
            Assert.IsTrue(expParams["d"].saved);
            Assert.IsFalse(expParams["e"].saved);
            Assert.IsTrue(expParams["f"].saved);
        }

        [Test]
        public void TestMultipleRemappings()
        {
            var av = CreateRoot("avatar");

            var parameters = av.AddComponent<ModularAvatarParameters>();

            parameters.parameters = new List<ParameterConfig>()
            {
                new ParameterConfig()
                {
                    nameOrPrefix = "a",
                    remapTo = "z",
                    syncType = ParameterSyncType.Float
                },
                new ParameterConfig()
                {
                    nameOrPrefix = "b",
                    remapTo = "z",
                    syncType = ParameterSyncType.Float
                },
            };

            var c1 = av.AddComponent<VRCContactReceiver>();
            c1.parameter = "a";
            
            var c2 = av.AddComponent<VRCContactReceiver>();
            c2.parameter = "a";

            var context = CreateContext(av);
            var maContext = context.ActivateExtensionContext<ModularAvatarContext>().BuildContext;
            
            var errors = ErrorReport.CaptureErrors(() => new RenameParametersHook().OnPreprocessAvatar(av, maContext));
            
            Assert.IsEmpty(errors);
            
            Assert.AreEqual("z", c1.parameter);
            Assert.AreEqual("z", c2.parameter);
        }
        
        [Test]
        public void TestMultipleRemappings_WithConflict()
        {
            var av = CreateRoot("avatar");

            var parameters = av.AddComponent<ModularAvatarParameters>();

            parameters.parameters = new List<ParameterConfig>()
            {
                new ParameterConfig()
                {
                    nameOrPrefix = "a",
                    remapTo = "z",
                    syncType = ParameterSyncType.Float
                },
                new ParameterConfig()
                {
                    nameOrPrefix = "b",
                    remapTo = "z",
                    syncType = ParameterSyncType.Int
                },
            };

            var context = CreateContext(av);
            var maContext = context.ActivateExtensionContext<ModularAvatarContext>().BuildContext;
            
            var errors = ErrorReport.CaptureErrors(() => new RenameParametersHook().OnPreprocessAvatar(av, maContext));
            
            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void ParameterOrderTest()
        {
            var av = CreateRoot("avatar");

            var rootMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var paramsAsset = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionsMenu = rootMenu;
            desc.expressionParameters = paramsAsset;

            var c1 = CreateChild(av, "a");
            var c2 = CreateChild(av, "b");
            var c3 = CreateChild(av, "c");
            var c4 = CreateChild(av, "d");

            AddParam(c1, "A");
            AddParam(c2, "B");
            AddParam(c3, "C");
            AddParam(c4, "D");
            
            AvatarProcessor.ProcessAvatar(av);

            paramsAsset = desc.expressionParameters;
            
            Assert.AreEqual("A", paramsAsset.parameters[0].name);
            Assert.AreEqual("B", paramsAsset.parameters[1].name);
            Assert.AreEqual("C", paramsAsset.parameters[2].name);
            Assert.AreEqual("D", paramsAsset.parameters[3].name);

            void AddParam(GameObject child, String name)
            {
                var param = child.AddComponent<ModularAvatarParameters>();
                param.parameters.Add(new ParameterConfig()
                {
                    nameOrPrefix = name,
                    syncType = ParameterSyncType.Float
                });
            }
        }

        [Test]
        public void RecursiveRenameTest()
        {
            var prefab = CreatePrefab("RecursiveRenameTest.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);

            var fx = (AnimatorController) FindFxController(prefab).animatorController;
            
            Assert.IsFalse(fx.parameters.Any(p => p.name.StartsWith("Hoge")));
            var fuga = fx.parameters.First(p => p.name.StartsWith("Fuga"));
            Assert.IsFalse(fx.parameters.Any(p => p.name.StartsWith("Fuga") && p.name != fuga.name));
            
            Assert.AreNotEqual(fuga.name, "Fuga"); // should be auto-renamed
        }
    }
}

#endif