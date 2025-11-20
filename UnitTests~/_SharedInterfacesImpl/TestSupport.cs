#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.ui;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace UnitTests.SharedInterfacesImpl
{
    public class TestSupport : ITestSupport
    {
        private const string MinimalAvatarGuid = "60d3416d1f6af4a47bf9056aefc38333";
        private static Dictionary<System.Type, string>? _scriptToDirectory = null;

        private Dictionary<Type, string> ScriptToDirectory
        {
            get
            {
                if (_scriptToDirectory != null) return _scriptToDirectory;
                
                _scriptToDirectory = new Dictionary<System.Type, string>();
                foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new string[] {"Packages/nadena.dev.modular-avatar/UnitTests"}))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var obj = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (obj != null && obj.GetClass() != null)
                    {
                        var assetDir = Path.GetDirectoryName(path);
                        if (assetDir != null)
                        {
                            _scriptToDirectory.Add(obj.GetClass(), assetDir);
                        }
                    }
                }

                return _scriptToDirectory;
            }
        }
        
        private TestSupport()
        {
        }

        [InitializeOnLoadMethod]
        static void Init()
        {
            ITestSupport.Instance = new TestSupport();
        }

        public void Setup()
        {
            ErrorReport.Clear();
            ErrorReportWindow.DISABLE_WINDOW = true;
        }

        public void Teardown()
        {
            ErrorReportWindow.DISABLE_WINDOW = false;
        }

        public void ProcessAvatar(GameObject gameObject)
        {
            using var scope = new OverrideTemporaryDirectoryScope(null);
            AvatarProcessor.ProcessAvatar(gameObject, PlatformRegistry.PlatformProviders[WellKnownPlatforms.VRChatAvatar30]);
        }

        public T LoadAsset<T>(Type relativeType, string relPath) where T : Object
        {
            var root = ScriptToDirectory.GetValueOrDefault(relativeType) ??
                       throw new Exception("Couldn't determine directory for type " + relativeType);
            root += "/";
            
            var path = root + relPath;

            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.NotNull(obj, "Missing test asset {0}", path);

            return obj;
        }

        public GameObject CreateTestAvatar(string name)
        {
            var path = AssetDatabase.GUIDToAssetPath(MinimalAvatarGuid);
            var go = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));

            return go;
        }

        public void ActivateFX(GameObject avatar)
        {
            var avDesc = avatar.GetComponent<VRCAvatarDescriptor>();
            var animator = avatar.GetComponent<Animator>();

            animator.runtimeAnimatorController = avDesc.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController;
        }
    }
}