#if MA_VRCSDK3_AVATARS

using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace UnitTests.SyncParameterSequence
{
    public class SyncParameterSequenceTest : TestBase
    {
        [Test]
        public void NonPrimaryPlatform()
        {
            ModularAvatarSyncParameterSequence.Platform platform;
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    platform = ModularAvatarSyncParameterSequence.Platform.PC;
                    break;
                default:
                    platform = ModularAvatarSyncParameterSequence.Platform.Android;
                    break;
            }
            
            var root = CreateRoot("root");
            var avdesc = root.GetComponent<VRCAvatarDescriptor>();

            var expParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            expParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter()
                {
                    name = "p1",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true,
                    defaultValue = 0.5f,
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "p2",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = true,
                    defaultValue = 0.5f,
                }
            };
            
            var refParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            refParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter()
                {
                    name = "p0",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "p2",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = true
                }
            };

            var c = avdesc.gameObject.AddComponent<ModularAvatarSyncParameterSequence>();
            c.PrimaryPlatform = platform;
            c.Parameters = refParams;
            
            avdesc.expressionParameters = expParams;

            var context = CreateContext(root);
            SyncParameterSequencePass.ExecuteStatic(context);

            expParams = avdesc.expressionParameters;
            
            Assert.AreEqual("p0", expParams.parameters[0].name);
            Assert.AreEqual("p2", expParams.parameters[1].name);
            Assert.AreEqual("p1", expParams.parameters[2].name);
            
            Assert.IsTrue(Mathf.Approximately(0f, expParams.parameters[0].defaultValue));
            Assert.IsTrue(Mathf.Approximately(0.5f, expParams.parameters[1].defaultValue));
            Assert.IsTrue(Mathf.Approximately(0.5f, expParams.parameters[2].defaultValue));
            
            Assert.AreEqual(2, refParams.parameters.Length);
        }
        
        [Test]
        public void PrimaryPlatform()
        {
            ModularAvatarSyncParameterSequence.Platform platform;
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    platform = ModularAvatarSyncParameterSequence.Platform.Android;
                    break;
                default:
                    platform = ModularAvatarSyncParameterSequence.Platform.PC;
                    break;
            }
            
            var root = CreateRoot("root");
            var avdesc = root.GetComponent<VRCAvatarDescriptor>();

            var expParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            expParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter()
                {
                    name = "p1",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true,
                    defaultValue = 0.5f,
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "p2",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = true,
                    defaultValue = 0.5f,
                },
                new VRCExpressionParameters.Parameter() {
                    name = "notsynced",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = false,
                }
            };
            
            var refParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            refParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter()
                {
                    name = "p0",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "p2",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = true
                }
            };

            var c = avdesc.gameObject.AddComponent<ModularAvatarSyncParameterSequence>();
            c.PrimaryPlatform = platform;
            c.Parameters = refParams;
            
            avdesc.expressionParameters = expParams;

            var context = CreateContext(root);
            SyncParameterSequencePass.ExecuteStatic(context);

            expParams = avdesc.expressionParameters;
            
            Assert.AreEqual("p0", expParams.parameters[0].name);
            Assert.AreEqual("p2", expParams.parameters[1].name);
            Assert.AreEqual("p1", expParams.parameters[2].name);
            Assert.AreEqual("notsynced", expParams.parameters[3].name);
            
            Assert.IsTrue(Mathf.Approximately(0f, expParams.parameters[0].defaultValue));
            Assert.IsTrue(Mathf.Approximately(0.5f, expParams.parameters[1].defaultValue));
            Assert.IsTrue(Mathf.Approximately(0.5f, expParams.parameters[2].defaultValue));
            
            Assert.AreEqual(3, refParams.parameters.Length);
            Assert.AreEqual("p0", refParams.parameters[0].name);
            Assert.AreEqual("p2", refParams.parameters[1].name);
            Assert.AreEqual("p1", refParams.parameters[2].name);
        }
        
        
    [Test]
    public void PrimaryPlatformOverflow()
    {
        ModularAvatarSyncParameterSequence.Platform platform;
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    platform = ModularAvatarSyncParameterSequence.Platform.Android;
                    break;
                default:
                    platform = ModularAvatarSyncParameterSequence.Platform.PC;
                    break;
            }
            
            var root = CreateRoot("root");
            var avdesc = root.GetComponent<VRCAvatarDescriptor>();

            var expParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            expParams.parameters = new[]
            {
                new VRCExpressionParameters.Parameter()
                {
                    name = "p1",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true,
                    defaultValue = 0.5f,
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "p2",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = true,
                    defaultValue = 0.5f,
                }
            };
            
            var refParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var paramList = new System.Collections.Generic.List<VRCExpressionParameters.Parameter>();
            for (int i = 0; i < VRCExpressionParameters.MAX_PARAMETER_COST; i++)
            {
                paramList.Add(new()
                {
                    name = "b" + i,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true
                });
            }

            refParams.parameters = paramList.ToArray();

            var c = avdesc.gameObject.AddComponent<ModularAvatarSyncParameterSequence>();
            c.PrimaryPlatform = platform;
            c.Parameters = refParams;
            
            avdesc.expressionParameters = expParams;

            var context = CreateContext(root);
            SyncParameterSequencePass.ExecuteStatic(context);

            expParams = avdesc.expressionParameters;
            
            Assert.AreEqual(2, expParams.parameters.Length);
            Assert.AreEqual("p1", expParams.parameters[0].name);
            Assert.AreEqual("p2", expParams.parameters[1].name);
            
            Assert.AreEqual(2, refParams.parameters.Length);
            Assert.AreEqual("p1", refParams.parameters[0].name);
            Assert.AreEqual("p2", refParams.parameters[1].name);
        }
    }
}

#endif