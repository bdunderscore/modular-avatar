using System;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor.SyncParameterSequence;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace UnitTests.SyncParameterSequence
{
    using P = VRCExpressionParameters.Parameter;
    
    public class ParameterInfoRegistryTest : TestBase
    {
        private ParameterInfoRegistry _registry;
        private int _inconsistentCallbackCounter;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            _registry = new ParameterInfoRegistry(new TestParameterInfoStore());
            _inconsistentCallbackCounter = 0;
            _registry.OnInconsistentBlueprintDetected += () => _inconsistentCallbackCounter++;
        }

        [Test]
        public void TestBasicNormalization()
        {
            var root = CreateRoot("root");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);
            
            Assert.AreEqual("a", descriptor.expressionParameters.parameters[0].name);
            Assert.AreEqual("b", descriptor.expressionParameters.parameters[1].name);
            Assert.AreEqual("c", descriptor.expressionParameters.parameters[2].name);
        }

        [Test]
        public void WhenBuildingPrimaryPlatformFirst_NotMarkedInconsistent()
        {
            var root = CreateRoot("root");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            Assert.AreEqual(0, _inconsistentCallbackCounter);
            Assert.AreEqual(0, _registry.InconsistentBlueprints.Count);
            
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });

            var errors = ErrorReport.CaptureErrors(() =>
            {
                _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);
            });
            
            Assert.IsEmpty(errors);
            Assert.AreEqual(0, _inconsistentCallbackCounter);
            Assert.AreEqual(0, _registry.InconsistentBlueprints.Count);
        }

        [Test]
        public void WhenExcessNonSyncedParams_DoesNotReportError()
        {
            var root = CreateRoot("root");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
                new P { name = "d", valueType = Float, networkSynced = false},
            });

            var errors = ErrorReport.CaptureErrors(() =>
            {
                _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);
            });
            
            Assert.IsEmpty(errors);
        }
        
        
        [Test]
        public void WhenExcessSyncedParams_DoesReportError()
        {
            var root = CreateRoot("root");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);
            
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
                new P { name = "d", valueType = Float },
            });

            var errors = ErrorReport.CaptureErrors(() =>
            {
                _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);
            });
            
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("error.syncparamsequence.unregistered_parameter", ((SimpleError)errors[0].TheError).TitleKey);
        }
        
        
        
        [Test]
        public void WhenTypeMismatch_DoesReportError()
        {
            var root = CreateRoot("root");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);
            
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Int },
            });

            var errors = ErrorReport.CaptureErrors(() =>
            {
                _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);
            });
            
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("error.syncparamsequence.type_mismatch", ((SimpleError)errors[0].TheError).TitleKey);
        }
        
        [Test]
        public void WhenBuildingSecondaryPlatformFirst_TemporarilyMarkedInconsistent()
        {
            var root = CreateRoot("root");
            var blueprintId = SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, false);

            Assert.AreEqual(1, _inconsistentCallbackCounter);
            Assert.AreEqual(1, _registry.InconsistentBlueprints.Count);
            Assert.AreEqual(blueprintId, _registry.InconsistentBlueprints.First());

            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
                new P { name = "b", valueType = Float },
            });

            _inconsistentCallbackCounter = 0;
            
            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, true);
            
            Assert.AreEqual(0, _inconsistentCallbackCounter);
            Assert.AreEqual(0, _registry.InconsistentBlueprints.Count);
        }

        [Test]
        public void CrossPlatform_PrimaryThenSecondary_MergeNoInconsistency()
        {
            var root = CreateRoot("root-merge-noincons");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            // Primary platform: a, c
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            // Secondary platform: only c
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "c", valueType = Float },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);

            // After merging, secondary descriptor should contain both a and c (sorted: a, c)
            var names = descriptor.expressionParameters.parameters.Select(p => p.name).ToArray();
            CollectionAssert.AreEqual(new[] { "a", "c" }, names);

            // No inconsistency should be reported
            Assert.AreEqual(0, _inconsistentCallbackCounter);
            Assert.AreEqual(0, _registry.InconsistentBlueprints.Count);
        }

        [Test]
        public void CrossPlatform_PrimaryThenSecondary_NewParam_Inconsistent()
        {
            var root = CreateRoot("root-newparam-incons");
            var blueprintId = SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            // Primary platform: a, c
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
                new P { name = "c", valueType = Float },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            // Secondary platform: c, d (introduces new parameter d)
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "c", valueType = Float },
                new P { name = "d", valueType = Float },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);

            // After merging, secondary descriptor should contain a, c, d (sorted: a, c, d)
            var names = descriptor.expressionParameters.parameters.Select(p => p.name).ToArray();
            CollectionAssert.AreEqual(new[] { "a", "c", "d" }, names);

            // Inconsistency should be reported for this blueprint
            Assert.AreEqual(1, _inconsistentCallbackCounter);
            Assert.AreEqual(1, _registry.InconsistentBlueprints.Count);
            Assert.IsTrue(_registry.InconsistentBlueprints.Contains(blueprintId));
        }

        [Test]
        public void CrossPlatform_SameName_DifferentValueType_Inconsistent_UsesPlatformValue()
        {
            var root = CreateRoot("root-valuetype-incons");
            var blueprintId = SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            // Primary platform: a as Float
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            // Secondary platform: a as Bool (conflicting valueType)
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Bool },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);

            // The descriptor used for the secondary build should keep the platform's valueType (Bool)
            var aParam = descriptor.expressionParameters.parameters.FirstOrDefault(p => p.name == "a");
            Assert.IsNotNull(aParam);
            Assert.AreEqual(Bool, aParam.valueType);

            // Inconsistency should be reported
            Assert.AreEqual(1, _inconsistentCallbackCounter);
            Assert.IsTrue(_registry.InconsistentBlueprints.Contains(blueprintId));
        }

        [Test]
        public void CrossPlatform_SameName_DifferentSaved_Consistent()
        {
            var root = CreateRoot("root-saved-consistent");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            // Primary platform: a saved = true
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float, saved = true },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            // Secondary platform: a saved = false (same name and type)
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float, saved = false },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);

            // Saved differences should NOT be considered inconsistent
            Assert.AreEqual(0, _inconsistentCallbackCounter);
            Assert.AreEqual(0, _registry.InconsistentBlueprints.Count);
        }

        [Test]
        public void CrossPlatform_SameName_DifferentDefaultValue_Inconsistent()
        {
            var root = CreateRoot("root-default-incons");
            var blueprintId = SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            // Primary platform: a defaultValue = 1
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float, defaultValue = 1f },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            // Secondary platform: a defaultValue = 2 (same name and type, different default)
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float, defaultValue = 2f },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);

            // Default value differences should be considered inconsistent
            Assert.AreEqual(1, _inconsistentCallbackCounter);
            Assert.IsTrue(_registry.InconsistentBlueprints.Contains(blueprintId));
        }

        [Test]
        public void CrossPlatform_PrimaryNonNetworkSynced_SecondaryEmpty_Consistent()
        {
            var root = CreateRoot("root-nonnetwork-consistent");
            SetRandomBlueprintId(root);

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            // Primary platform: a but not networkSynced
            descriptor.expressionParameters = CreateParams(new[]
            {
                new P { name = "a", valueType = Float, networkSynced = false },
            });

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.Android, true);

            // Secondary platform: no parameters
            descriptor.expressionParameters = CreateParams(Array.Empty<P>());

            _registry.NormalizeParameters(CreateContext(root), descriptor, BuildTarget.StandaloneWindows64, false);

            // Should be consistent, and secondary should have no parameters
            Assert.AreEqual(0, _inconsistentCallbackCounter);
            Assert.AreEqual(0, _registry.InconsistentBlueprints.Count);
            Assert.IsTrue(descriptor.expressionParameters.parameters == null || descriptor.expressionParameters.parameters.Length == 0);
        }

        private VRCExpressionParameters CreateParams(VRCExpressionParameters.Parameter[] p)
        {
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = p;
            return parameters;
        }

        private string SetRandomBlueprintId(GameObject root)
        {
            var pipelineManager = root.GetComponent<PipelineManager>();
            return pipelineManager.blueprintId = "avtr-" + GUID.Generate();
        }
    }
}