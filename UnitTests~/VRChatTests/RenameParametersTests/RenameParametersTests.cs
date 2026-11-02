#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using AvatarProcessor = nadena.dev.modular_avatar.core.editor.AvatarProcessor;

namespace nadena.dev.modular_avatar.test.Tests.RenameParametersTests
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
        public void RenameParametersRenamesTransitionParameters()
        {
            var prefab = CreatePrefab("RenamesTransitionParameters.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            var layer = findFxLayer(prefab, "test");
            var transition = layer.stateMachine.states[0].state.transitions[0];
            Assert.AreEqual("y", transition.conditions[0].parameter);
        }
    
        [Test]
        public void RenameInstalledMenu()
        {
            var prefab = CreatePrefab("RenameInstalledMenu.prefab");
            AvatarProcessor.ProcessAvatar(prefab);
    
            var menu = prefab.GetComponent<VRCAvatarDescriptor>().expressionsMenu;
            Assert.AreEqual("test$33bf6fbd7cd8", menu.controls[0].parameter.name);
            Assert.AreEqual("test$33bf6fbd7cd8", menu.controls[1].subMenu.controls[0].parameter.name);
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
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            
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
            Assert.AreEqual("a$014659ab9d98", valueConflict.DetailsSubst[0]);
            Assert.AreEqual("0", valueConflict.DetailsSubst[1]);
            Assert.AreEqual("1", valueConflict.DetailsSubst[2]);
            
            Assert.AreEqual("Conflict/P", valueConflict.References[0].Path);
            Assert.AreEqual("Conflict/P (1)", valueConflict.References[1].Path);
            
            var typeConflict = errors
                .Select(e => e.TheError)
                .Cast<SimpleError>()
                .First(e => e.TitleKey == "error.rename_params.type_conflict");
            
            Assert.AreEqual("a$88c01afc056a", typeConflict.DetailsSubst[0]);
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
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            
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
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            
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
    
        [Test]
        public void AnimatorOnlyAndLocalParameterShouldNotSync()
        {
            var root = CreateRoot("x");
            
            var c1 = CreateChild(root, "c1");
            var c2 = CreateChild(root, "c2");
            
            var p1 = c1.AddComponent<ModularAvatarParameters>();
            var p2 = c2.AddComponent<ModularAvatarParameters>();
    
            p1.parameters = new()
            {
                new()
                {
                    nameOrPrefix = "a",
                    syncType = ParameterSyncType.NotSynced,
                    localOnly = false
                }
            };
            p2.parameters = new()
            {
                new()
                {
                    nameOrPrefix = "a",
                    syncType = ParameterSyncType.Int,
                    localOnly = true
                }
            };
            
            AvatarProcessor.ProcessAvatar(root);
            
            var expParams = root.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters
                .Select(p => new KeyValuePair<string, VRCExpressionParameters.Parameter>(p.name, p))
                .ToImmutableDictionary();
            
            Assert.IsFalse(expParams["a"].networkSynced);
        }
    
        [Test]
        public void RenamesParameterCurves_InMergeAnimator()
        {
            var prefab = CreatePrefab("RenamesParameterCurves.prefab");
            AvatarProcessor.ProcessAvatar(prefab);
            
            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController as AnimatorController;
    
            var state = fx.layers.First(l => l.name == "test").stateMachine.defaultState;
            var motion = (AnimationClip)state.motion;
    
            var bindings = AnimationUtility.GetCurveBindings(motion);
            var theBinding = bindings[0];
            Assert.AreEqual("test2", theBinding.propertyName);
        }
        
        [Test]
        public void RenamesParameterCurves_InMergeMotion()
        {
            var prefab = CreatePrefab("RenamesParameterCurves.prefab");
            AvatarProcessor.ProcessAvatar(prefab);
            
            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController as AnimatorController;
    
            var state = fx.layers[0].stateMachine.defaultState;
            var tree = (BlendTree)state.motion;
            var motion = (AnimationClip)tree.children[0].motion;
    
            var bindings = AnimationUtility.GetCurveBindings(motion);
            var theBinding = bindings[0];
            Assert.AreEqual("test2", theBinding.propertyName);
        }
    
        [Test]
        public void WhenVirtualizedComponentsAreNull_DoesNotThrow()
        {
            var prefab = CreatePrefab("NullVirtualizedComponents.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
        }

        #if MA_VRCSDK3_AVATARS_3_10_6_OR_NEWER
        [Test]
        public void AdvancedOptions_AppliedToNewParameters()
        {
            var root = CreateRoot("root");

            root.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new()
                {
                    nameOrPrefix = "int",
                    syncType = ParameterSyncType.Int,
                    bitWidth = 4,
                    representation = ParameterRepresentation.Signed,
                    useCustomLerpDuration = true,
                    customLerpDuration = 2f,
                },
                new()
                {
                    nameOrPrefix = "float",
                    syncType = ParameterSyncType.Float,
                    representation = ParameterRepresentation.Unsigned,
                },
            };

            var (errors, expParams, _) = RunRenameParameters(root);

            Assert.IsEmpty(errors);
            Assert.AreEqual(4, expParams["int"].bitWidth);
            Assert.IsTrue(expParams["int"].flippedRepresentation);
            Assert.IsTrue(expParams["int"].useCustomLerpDuration);
            Assert.AreEqual(2f, expParams["int"].customLerpDuration);
            Assert.IsTrue(expParams["float"].flippedRepresentation);
            Assert.AreEqual(VRCExpressionParameters.Parameter.ValueRepresentation.Unsigned,
                expParams["float"].GetRepresentationByType());
        }

        [Test]
        public void AdvancedOptions_UnspecifiedUsesSdkDefaults()
        {
            var root = CreateRoot("root");

            root.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new() { nameOrPrefix = "a", syncType = ParameterSyncType.Int },
            };

            var (errors, expParams, _) = RunRenameParameters(root);

            Assert.IsEmpty(errors);
            Assert.AreEqual(8, expParams["a"].bitWidth);
            Assert.IsFalse(expParams["a"].flippedRepresentation);
            Assert.IsFalse(expParams["a"].useCustomLerpDuration);
        }

        [Test]
        public void AdvancedOptions_SiblingConflictsAreReported()
        {
            var root = CreateRoot("root");
            var c1 = CreateChild(root, "c1");
            var c2 = CreateChild(root, "c2");

            c1.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new()
                {
                    nameOrPrefix = "a",
                    syncType = ParameterSyncType.Int,
                    bitWidth = 4,
                    representation = ParameterRepresentation.Signed,
                    useCustomLerpDuration = true,
                    customLerpDuration = 1f,
                },
            };
            c2.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new()
                {
                    nameOrPrefix = "a",
                    syncType = ParameterSyncType.Int,
                    bitWidth = 6,
                    representation = ParameterRepresentation.Unsigned,
                    useCustomLerpDuration = true,
                    customLerpDuration = 2f,
                },
            };

            var (errors, expParams, _) = RunRenameParameters(root);

            var titleKeys = errors.Select(e => e.TheError).OfType<SimpleError>().Select(e => e.TitleKey).ToList();
            Assert.That(titleKeys, Does.Contain("error.rename_params.bit_width_conflict"));
            Assert.That(titleKeys, Does.Contain("error.rename_params.representation_conflict"));
            Assert.That(titleKeys, Does.Contain("error.rename_params.lerp_duration_conflict"));

            Assert.AreEqual(4, expParams["a"].bitWidth);
            Assert.IsTrue(expParams["a"].flippedRepresentation);
            Assert.AreEqual(1f, expParams["a"].customLerpDuration);
        }

        [Test]
        public void AdvancedOptions_DefaultValueIsClampedToRange()
        {
            var root = CreateRoot("root");

            root.AddComponent<ModularAvatarParameters>().parameters = new()
            {
                new()
                {
                    nameOrPrefix = "unsigned",
                    syncType = ParameterSyncType.Int,
                    bitWidth = 4,
                    defaultValue = 200,
                    hasExplicitDefaultValue = true,
                    m_overrideAnimatorDefaults = true,
                },
                new()
                {
                    nameOrPrefix = "signed",
                    syncType = ParameterSyncType.Int,
                    bitWidth = 4,
                    representation = ParameterRepresentation.Signed,
                    defaultValue = -20,
                    hasExplicitDefaultValue = true,
                    m_overrideAnimatorDefaults = true,
                },
                new()
                {
                    nameOrPrefix = "float",
                    syncType = ParameterSyncType.Float,
                    representation = ParameterRepresentation.Unsigned,
                    defaultValue = -0.5f,
                    hasExplicitDefaultValue = true,
                    m_overrideAnimatorDefaults = true,
                },
            };

            var (errors, expParams, context) = RunRenameParameters(root);

            Assert.IsEmpty(errors);
            Assert.AreEqual(15f, expParams["unsigned"].defaultValue);
            Assert.AreEqual(-8f, expParams["signed"].defaultValue);
            Assert.AreEqual(0f, expParams["float"].defaultValue);

            var overrides = context.GetState<DefaultValues>().InitialValueOverrides;
            Assert.AreEqual(15f, overrides["unsigned"]);
            Assert.AreEqual(-8f, overrides["signed"]);
            Assert.AreEqual(0f, overrides["float"]);
        }

        private (List<ErrorContext>, Dictionary<string, VRCExpressionParameters.Parameter>, nadena.dev.ndmf.BuildContext)
            RunRenameParameters(GameObject root)
        {
            var context = CreateContext(root);
            var maContext = context.ActivateExtensionContext<ModularAvatarContext>().BuildContext;
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            var errors = ErrorReport.CaptureErrors(() => new RenameParametersHook().OnPreprocessAvatar(root, maContext));
            var expParams = root.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters
                .ToDictionary(p => p.name);

            return (errors, expParams, context);
        }
        #endif

        [Test]
        public void RemapsContactReceiverPrefixParameters()
        {
            var root = CreateRoot("avatar");
            var parameters = root.AddComponent<ModularAvatarParameters>();
            parameters.parameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    nameOrPrefix = "pb",
                    remapTo = "pb2",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                }
            };

            var contact = root.AddComponent<VRCContactReceiver>();
            contact.parameter = "pb";

            AvatarProcessor.ProcessAvatar(root);

            Assert.That(contact.parameter, Is.EqualTo("pb2"));
        }

        [TestCase(false, "animator-target")]
        [TestCase(true, "prefix-target")]
        public void ContactReceiverUsesPrefixMappingBeforeAnimatorMappingWithoutCascading(
            bool includePrefixMapping, string expectedName)
        {
            var root = CreateRoot("avatar");
            var parameters = root.AddComponent<ModularAvatarParameters>();
            parameters.parameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    nameOrPrefix = "contact",
                    remapTo = "animator-target",
                    syncType = ParameterSyncType.Float
                },
                new ParameterConfig
                {
                    nameOrPrefix = "prefix-target",
                    remapTo = "cascaded-target",
                    syncType = ParameterSyncType.Float
                }
            };
            if (includePrefixMapping)
            {
                parameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = "contact",
                    remapTo = "prefix-target",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                });
            }

            var contact = root.AddComponent<VRCContactReceiver>();
            contact.parameter = "contact";

            AvatarProcessor.ProcessAvatar(root);

            Assert.That(contact.parameter, Is.EqualTo(expectedName));
        }

        [Test]
        public void RemapsExactPhysBonePrefixNameInAnimatorControllers()
        {
            AssertControllerParameterAndTransitionAreRemapped("pb", "pb2");
        }

        [Test]
        public void ExplicitAnimatorMappingTakesPrecedenceOverExactPhysBonePrefixMapping()
        {
            AssertControllerParameterAndTransitionAreRemapped(
                "pb",
                "explicit-target",
                new ParameterConfig
                {
                    nameOrPrefix = "pb",
                    remapTo = "explicit-target",
                    syncType = ParameterSyncType.Bool
                }
            );
        }

        [TestCase("pbOther")]
        [TestCase("pb2")]
        public void DoesNotRemapNamesThatOnlyStartWithPhysBonePrefix(string parameterName)
        {
            AssertControllerParameterAndTransitionAreRemapped(parameterName, parameterName);
        }

        [TestCase("_IsPosed")]
        [TestCase("_SenderType")]
        [TestCase("_SenderLocalPositionX")]
        [TestCase("_SenderLocalPositionY")]
        [TestCase("_SenderLocalPositionZ")]
        [TestCase("_SenderLossyScaleX")]
        [TestCase("_SenderLossyScaleY")]
        [TestCase("_SenderLossyScaleZ")]
        [TestCase("_SenderVelocityX")]
        [TestCase("_SenderVelocityY")]
        [TestCase("_SenderVelocityZ")]
        [TestCase("_SenderVelocityMagnitude")]
        [TestCase("_PointSeparation")]
        [TestCase("_ProximitySpeed")]
        public void RemapsPhysBoneSuffixesInAnimatorControllers(string suffix)
        {
            var originalName = "pb" + suffix;
            var remappedName = "pb2" + suffix;

            AssertControllerParameterAndTransitionAreRemapped(originalName, remappedName);
        }

        [TestCase("NormalTag")]
        [TestCase("tag with_spaces")]
        [TestCase("")]
        public void RemapsCollisionTagsInAnimatorControllers(string tag)
        {
            var originalName = "pb_CollisionTag_" + tag;
            var remappedName = "pb2_CollisionTag_" + tag;

            AssertControllerParameterAndTransitionAreRemapped(originalName, remappedName);
        }

        [TestCase("pb_CollisionTag")]
        [TestCase("other_CollisionTag_NormalTag")]
        public void DoesNotRemapUnrelatedCollisionTagNames(string parameterName)
        {
            AssertControllerParameterAndTransitionAreRemapped(parameterName, parameterName);
        }

        [Test]
        public void ExplicitAnimatorMappingTakesPrecedenceOverCollisionTagPrefixMapping()
        {
            AssertControllerParameterAndTransitionAreRemapped(
                "pb_CollisionTag_NormalTag",
                "explicit-collision-tag",
                new ParameterConfig
                {
                    nameOrPrefix = "pb_CollisionTag_NormalTag",
                    remapTo = "explicit-collision-tag",
                    syncType = ParameterSyncType.Bool
                }
            );
        }

        private void AssertControllerParameterAndTransitionAreRemapped(
            string originalName,
            string expectedName,
            params ParameterConfig[] additionalMappings
        )
        {
            var root = CreateRoot("avatar");
            var parameters = root.AddComponent<ModularAvatarParameters>();
            parameters.parameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    nameOrPrefix = "pb",
                    remapTo = "pb2",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                }
            };
            parameters.parameters.AddRange(additionalMappings);

            var controller = TrackObject(new AnimatorController
            {
                parameters = new[]
                {
                    new AnimatorControllerParameter
                    {
                        name = originalName,
                        type = AnimatorControllerParameterType.Bool
                    }
                }
            });
            var stateMachine = TrackObject(new AnimatorStateMachine());
            var initialState = TrackObject(stateMachine.AddState("Initial"));
            var destinationState = TrackObject(stateMachine.AddState("Destination"));
            stateMachine.defaultState = initialState;
            var transition = TrackObject(initialState.AddTransition(destinationState));
            transition.AddCondition(AnimatorConditionMode.If, 0, originalName);
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Parameter remap",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };

            var mergeAnimator = root.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = controller;

            AvatarProcessor.ProcessAvatar(root);

            var outputController = FindFxController(root).animatorController as AnimatorController;
            Assert.NotNull(outputController);
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Contain(expectedName));
            if (originalName != expectedName)
            {
                Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Not.Contain(originalName));
            }

            var outputLayer = outputController.layers.Single(layer => layer.name == "Parameter remap");
            var outputTransition = outputLayer.stateMachine.defaultState.transitions.Single();
            Assert.That(
                outputTransition.conditions.Select(condition => condition.parameter),
                Is.EqualTo(new[] { expectedName })
            );
        }

        [TestCase(true, 1f)]
        [TestCase(false, 42f)]
        public void ConvergentMixedAnimatorParameterTypesPromoteToFloat(bool boolFirst, float expectedDefault)
        {
            var source = TrackObject(new AnimatorController
            {
                parameters = new[]
                {
                    new AnimatorControllerParameter
                    {
                        name = "alpha",
                        type = boolFirst ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Int,
                        defaultBool = true,
                        defaultInt = 42
                    },
                    new AnimatorControllerParameter
                    {
                        name = "bravo",
                        type = boolFirst ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Bool,
                        defaultBool = true,
                        defaultInt = 42
                    }
                }
            });

            var output = ProcessRenamedMergedController(source,
                ("alpha", "target"),
                ("bravo", "target"));

            var parameter = output.Parameters["target"];
            Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(parameter.defaultFloat, Is.EqualTo(expectedDefault));
            Assert.That(source.parameters.Select(parameter => parameter.type), Is.EqualTo(new[]
            {
                boolFirst ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Int,
                boolFirst ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Bool
            }));
            Assert.That(source.parameters[0].defaultBool, Is.True);
            Assert.That(source.parameters[1].defaultInt, Is.EqualTo(42));
        }

        [Test]
        public void ThreeWayConvergentAnimatorParameterTypesPreserveFirstFloatDefault()
        {
            var source = TrackObject(new AnimatorController
            {
                // Parameter coalescing uses explicit ordinal source-name order, not source insertion order.
                parameters = new[]
                {
                    new AnimatorControllerParameter
                    {
                        name = "charlieBool",
                        type = AnimatorControllerParameterType.Bool,
                        defaultBool = false
                    },
                    new AnimatorControllerParameter
                    {
                        name = "bravoInt",
                        type = AnimatorControllerParameterType.Int,
                        defaultInt = 42
                    },
                    new AnimatorControllerParameter
                    {
                        name = "alphaFloat",
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = 0.75f
                    }
                }
            });

            var output = ProcessRenamedMergedController(source,
                ("alphaFloat", "target"),
                ("bravoInt", "target"),
                ("charlieBool", "target"));

            var parameter = output.Parameters["target"];
            Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(parameter.defaultFloat, Is.EqualTo(0.75f));
            Assert.That(source.parameters.Select(parameter => parameter.type), Is.EqualTo(new[]
            {
                AnimatorControllerParameterType.Bool,
                AnimatorControllerParameterType.Int,
                AnimatorControllerParameterType.Float
            }));
            Assert.That(source.parameters[2].defaultFloat, Is.EqualTo(0.75f));
        }

        [Test]
        public void ConvergentMixedAnimatorParameterTypesCorrectParameterDrivers()
        {
            var avatar = CreateRoot("avatar");
            avatar.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                Mapping("alpha", "target"),
                Mapping("bravo", "target")
            };

            var source = TrackObject(new AnimatorController
            {
                parameters = new[]
                {
                    new AnimatorControllerParameter
                    {
                        name = "alpha",
                        type = AnimatorControllerParameterType.Bool,
                        defaultBool = true
                    },
                    new AnimatorControllerParameter
                    {
                        name = "bravo",
                        type = AnimatorControllerParameterType.Int,
                        defaultInt = 42
                    }
                }
            });
            var stateMachine = TrackObject(new AnimatorStateMachine());
            var state = stateMachine.AddState("Default");
            stateMachine.defaultState = state;
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRCAvatarParameterDriver.Parameter>
            {
                new()
                {
                    name = "alpha",
                    type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Add,
                    value = 1
                }
            };
            source.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Driver correction",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };
            avatar.AddComponent<ModularAvatarMergeAnimator>().animator = source;

            AvatarProcessor.ProcessAvatar(avatar);

            var output = FindFxController(avatar).animatorController as AnimatorController;
            var outputDriver = output.layers.Single(layer => layer.name == "Driver correction").stateMachine.defaultState
                .behaviours.OfType<VRCAvatarParameterDriver>().Single();
            var operation = outputDriver.parameters.Single(parameter =>
                parameter.type == VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Add);
            Assert.That(operation.name, Is.Not.EqualTo("alpha"));
            Assert.That(operation.name, Is.Not.EqualTo("target"));
            Assert.That(outputDriver.parameters, Has.Count.EqualTo(3));
            var copies = outputDriver.parameters.Where(parameter =>
                parameter.type == VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy).ToList();
            Assert.That(copies, Has.Count.EqualTo(2));
            var copyToTemporary = copies.Single(parameter => parameter.name == operation.name);
            Assert.That(copyToTemporary.source, Is.EqualTo("target"));
            var copyToTarget = copies.Single(parameter => parameter.name == "target");
            Assert.That(copyToTarget.source, Is.EqualTo(operation.name));
        }

        [Test]
        public void SwappedAnimatorParameterCurvesKeepBothCurveData()
        {
            var avatar = CreateRoot("avatar");
            // Mappings are composed sequentially, so a temporary name is required to express a true swap.
            avatar.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                Mapping("swapTemporary", "a"),
                Mapping("a", "b"),
                Mapping("b", "swapTemporary")
            };

            var sourceClip = TrackObject(new AnimationClip());
            var aCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f, 1f, 2f),
                new Keyframe(0.5f, 0.75f, 3f, 4f)
            );
            var bCurve = new AnimationCurve(
                new Keyframe(0f, 10f, 5f, 6f),
                new Keyframe(1f, 20f, 7f, 8f)
            );
            sourceClip.SetCurve("", typeof(Animator), "a", aCurve);
            sourceClip.SetCurve("", typeof(Animator), "b", bCurve);

            var source = TrackObject(new AnimatorController());
            var stateMachine = TrackObject(new AnimatorStateMachine());
            stateMachine.defaultState = stateMachine.AddState("Default");
            stateMachine.defaultState.motion = sourceClip;
            source.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Swapped curves",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };
            avatar.AddComponent<ModularAvatarMergeAnimator>().animator = source;

            AvatarProcessor.ProcessAvatar(avatar);

            var output = FindFxController(avatar).animatorController as AnimatorController;
            var outputClip = output.layers.Single(layer => layer.name == "Swapped curves").stateMachine.defaultState
                .motion as AnimationClip;
            Assert.That(AnimationUtility.GetCurveBindings(outputClip).Select(binding => binding.propertyName),
                Is.EquivalentTo(new[] { "a", "b" }));
            AssertParameterCurve(outputClip, "a", bCurve.keys);
            AssertParameterCurve(outputClip, "b", aCurve.keys);

            AssertParameterCurve(sourceClip, "a", aCurve.keys);
            AssertParameterCurve(sourceClip, "b", bCurve.keys);
        }

        [Test]
        public void ChainedAnimatorParameterCurvesKeepBothCurveData()
        {
            var avatar = CreateRoot("avatar");
            avatar.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                Mapping("a", "b"),
                Mapping("b", "c")
            };

            var sourceClip = TrackObject(new AnimationClip());
            var aCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 2f));
            var bCurve = new AnimationCurve(new Keyframe(0f, 3f), new Keyframe(1f, 4f));
            sourceClip.SetCurve("", typeof(Animator), "a", aCurve);
            sourceClip.SetCurve("", typeof(Animator), "b", bCurve);

            var source = TrackObject(new AnimatorController());
            var stateMachine = TrackObject(new AnimatorStateMachine());
            stateMachine.defaultState = stateMachine.AddState("Default");
            stateMachine.defaultState.motion = sourceClip;
            source.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Chained curves",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };
            avatar.AddComponent<ModularAvatarMergeAnimator>().animator = source;

            AvatarProcessor.ProcessAvatar(avatar);

            var output = FindFxController(avatar).animatorController as AnimatorController;
            var outputClip = output.layers.Single(layer => layer.name == "Chained curves").stateMachine.defaultState
                .motion as AnimationClip;
            Assert.That(AnimationUtility.GetCurveBindings(outputClip).Select(binding => binding.propertyName),
                Is.EquivalentTo(new[] { "b", "c" }));
            AssertParameterCurve(outputClip, "b", aCurve.keys);
            AssertParameterCurve(outputClip, "c", bCurve.keys);

            AssertParameterCurve(sourceClip, "a", aCurve.keys);
            AssertParameterCurve(sourceClip, "b", bCurve.keys);
        }

        [Test]
        public void RemapsStandaloneMergeMotionBlendTreeAndAnimatorCurves()
        {
            var avatar = CreateRoot("avatar");
            var child = CreateChild(avatar, "child");
            child.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                Mapping("blendX", "remappedX"),
                Mapping("blendY", "remappedY"),
                Mapping("direct", "remappedDirect"),
                Mapping("curve", "remappedCurve")
            };

            var source = CreateParameterizedMotion("blendX", "blendY", "direct", "curve");
            child.AddComponent<ModularAvatarMergeBlendTree>().Motion = source;

            AvatarProcessor.ProcessAvatar(avatar);

            var output = FindBlendTree(FindFxController(avatar).animatorController as AnimatorController, "remappedX");
            Assert.NotNull(output);
            Assert.That(output.blendParameterY, Is.EqualTo("remappedY"));
            var direct = output.children.Single().motion as BlendTree;
            Assert.NotNull(direct);
            Assert.That(direct.children.Single().directBlendParameter, Is.EqualTo("remappedDirect"));
            var outputClip = direct.children.Single().motion as AnimationClip;
            Assert.That(AnimationUtility.GetCurveBindings(outputClip)
                .Select(binding => binding.propertyName), Does.Contain("remappedCurve"));

            Assert.That(source.blendParameter, Is.EqualTo("blendX"));
            Assert.That(source.blendParameterY, Is.EqualTo("blendY"));
            Assert.That(((BlendTree)source.children.Single().motion).children.Single().directBlendParameter,
                Is.EqualTo("direct"));
            Assert.That(AnimationUtility.GetCurveBindings(
                    (AnimationClip)((BlendTree)source.children.Single().motion).children.Single().motion)
                .Select(binding => binding.propertyName), Does.Contain("curve"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemapsStandaloneMergeMotionCollisionTagParameters(bool autoRename)
        {
            var avatar = CreateRoot("avatar");
            var child = CreateChild(avatar, "child");
            child.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = "pb",
                    remapTo = "pb2",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                }
            };
            child.AddComponent<ModularAvatarRenameVRChatCollisionTags>().configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Tag", autoRename = autoRename, renameTo = autoRename ? "" : "ManualTag" }
            };
            var receiver = child.AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { "Tag" };
            child.AddComponent<ModularAvatarMergeBlendTree>().Motion =
                CreateParameterizedMotion("pb_CollisionTag_Tag", "unusedY", "unusedDirect", "unusedCurve");

            AvatarProcessor.ProcessAvatar(avatar);

            var output = FindBlendTree(FindFxController(avatar).animatorController as AnimatorController,
                "pb2_CollisionTag_" + receiver.collisionTags.Single());
            Assert.NotNull(output);
            Assert.That(output.blendParameter, Is.EqualTo("pb2_CollisionTag_" + receiver.collisionTags.Single()));
            if (autoRename)
            {
                Assert.That(receiver.collisionTags.Single(), Does.Match(@"^Tag\$[0-9a-f-]+$"));
            }
            else
            {
                Assert.That(receiver.collisionTags.Single(), Is.EqualTo("ManualTag"));
            }
        }

        [Test]
        public void AppliesScopedMappingsToSharedStandaloneMotionWithoutChangingSource()
        {
            var avatar = CreateRoot("avatar");
            var source = CreateParameterizedMotion("parameter", "y", "direct", "curve");
            foreach (var (name, target) in new[] { ("first", "firstRemapped"), ("second", "secondRemapped") })
            {
                var child = CreateChild(avatar, name);
                child.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig> { Mapping("parameter", target) };
                child.AddComponent<ModularAvatarMergeBlendTree>().Motion = source;
            }

            AvatarProcessor.ProcessAvatar(avatar);

            var outputController = FindFxController(avatar).animatorController as AnimatorController;
            Assert.NotNull(FindBlendTree(outputController, "firstRemapped"));
            Assert.NotNull(FindBlendTree(outputController, "secondRemapped"));
            Assert.That(source.blendParameter, Is.EqualTo("parameter"));
        }

        [Test]
        public void ReportsAmbiguousCollisionTagParametersInStandaloneMergeMotion()
        {
            const string parameter = "pb_CollisionTag_Tag_CollisionTag_Extra";
            var avatar = CreateRoot("avatar");
            var child = CreateChild(avatar, "child");
            child.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = "pb",
                    remapTo = "pb2",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                }
            };
            child.AddComponent<ModularAvatarMergeBlendTree>().Motion =
                CreateParameterizedMotion(parameter, "y", "direct", "curve");

            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(avatar));

            Assert.That(errors.Select(error => error.TheError).OfType<SimpleError>()
                .Where(error => error.TitleKey == "error.rename_collision_tags.ambiguous_parameter")
                .Select(error => error.DetailsSubst[0]), Does.Contain(parameter));
        }

        private VirtualAnimatorController ProcessRenamedMergedController(AnimatorController source,
            params (string source, string destination)[] mappings)
        {
            var avatar = CreateRoot("avatar");
            avatar.AddComponent<ModularAvatarParameters>().parameters = mappings
                .Select(mapping => Mapping(mapping.source, mapping.destination))
                .ToList();
            var mergeAnimator = avatar.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = source;

            var context = CreateContext(avatar);
            var maContext = context.ActivateExtensionContext<ModularAvatarContext>().BuildContext;
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            new RenameParametersHook().OnPreprocessAvatar(avatar, maContext);

            return animatorServices.ControllerContext.Controllers[mergeAnimator];
        }

        private static void AssertParameterCurve(AnimationClip clip, string parameter, Keyframe[] expected)
        {
            var curve = AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Animator), parameter));
            Assert.That(curve, Is.Not.Null);
            Assert.That(curve.keys, Has.Length.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(curve.keys[i].time, Is.EqualTo(expected[i].time));
                Assert.That(curve.keys[i].value, Is.EqualTo(expected[i].value));
                Assert.That(curve.keys[i].inTangent, Is.EqualTo(expected[i].inTangent));
                Assert.That(curve.keys[i].outTangent, Is.EqualTo(expected[i].outTangent));
            }
        }

        private static ParameterConfig Mapping(string source, string target) => new()
        {
            nameOrPrefix = source,
            remapTo = target,
            syncType = ParameterSyncType.Float
        };

        private BlendTree CreateParameterizedMotion(string blendX, string blendY, string directParameter,
            string curveParameter)
        {
            var clip = TrackObject(new AnimationClip());
            clip.SetCurve("", typeof(Animator), curveParameter, AnimationCurve.Constant(0, 1, 1));
            var direct = TrackObject(new BlendTree { blendType = BlendTreeType.Direct });
            direct.AddChild(clip, 1);
            var directChildren = direct.children;
            directChildren[0].directBlendParameter = directParameter;
            direct.children = directChildren;
            var root = TrackObject(new BlendTree
            {
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = blendX,
                blendParameterY = blendY
            });
            root.AddChild(direct, 1);
            return root;
        }

        private static BlendTree FindBlendTree(AnimatorController controller, string blendParameter)
        {
            return controller.layers.SelectMany(layer => layer.stateMachine.states)
                .Select(state => FindBlendTree(state.state.motion, blendParameter))
                .FirstOrDefault(tree => tree != null);
        }

        private static BlendTree FindBlendTree(Motion motion, string blendParameter)
        {
            if (motion is not BlendTree tree) return null;
            if (tree.blendParameter == blendParameter) return tree;
            return tree.children.Select(child => FindBlendTree(child.motion, blendParameter))
                .FirstOrDefault(child => child != null);
        }

        #if MA_VRCSDK3_AVATARS_3_10_3_OR_NEWER
        [Test]
        public void Remaps_PhysBone_And_Raycast()
        {
            var prefab = CreatePrefab("RaycastAndPhysBone/RaycastAndPhysBone.prefab");
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            var transition = findFxLayer(prefab, "test").stateMachine.defaultState.transitions[0];
            var transitionParams = transition.conditions.Select(c => c.parameter).ToList();
            
            Assert.That(transitionParams, Does.Not.Contain("pb2_Unmapped"));
            Assert.That(transitionParams, Does.Not.Contain("rc2_Unmapped"));
            
            Assert.That(transitionParams, Does.Contain("rc2_Hit"));
            Assert.That(transitionParams, Does.Contain("rc2_Ratio"));
            Assert.That(transitionParams, Does.Contain("rc2_Distance"));
            Assert.That(transitionParams, Does.Contain("pb2_IsGrabbed"));
            Assert.That(transitionParams, Does.Contain("pb2_Angle"));
            Assert.That(transitionParams, Does.Contain("pb2_Stretch"));
            Assert.That(transitionParams, Does.Contain("pb2_Squish"));
    
            var pb = prefab.GetComponentInChildren<VRCPhysBone>();
            Assert.That(pb.parameter, Is.EqualTo("pb2"));
            
            var rc = prefab.GetComponentInChildren<VRCRaycast>();
            Assert.That(rc.Parameter, Is.EqualTo("rc2"));
        }
        #endif
    }
}

#endif