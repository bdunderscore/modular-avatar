using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace modular_avatar_tests
{
    public class TestBase
    {
        private const string TEMP_ASSET_PATH = "Assets/ZZZ_Temp";
        private static Dictionary<System.Type, string> _scriptToDirectory = null;

        private List<GameObject> objects;

        [SetUp]
        public virtual void Setup()
        {
            ITestSupport.Instance.Setup();

            objects = new List<GameObject>();
        }

        [TearDown]
        public virtual void Teardown()
        {
            foreach (var obj in objects)
            {
                Object.DestroyImmediate(obj);
            }

            AssetDatabase.DeleteAsset(TEMP_ASSET_PATH);
            FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
            
            ITestSupport.Instance.Teardown();
        }

        protected nadena.dev.ndmf.BuildContext CreateContext(GameObject root)
        {
            return new nadena.dev.ndmf.BuildContext(root, TEMP_ASSET_PATH); // TODO - cleanup
        }

        protected GameObject CreateRoot(string name)
        {
            var obj = ITestSupport.Instance.CreateTestAvatar(name);
            objects.Add(obj);
            return obj;
        }

        protected GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.parent = parent.transform;
            objects.Add(go);
            return go;
        }

        protected GameObject CreatePrefab(string relPath)
        {
            var prefab = LoadAsset<GameObject>(relPath);

            var go = Object.Instantiate(prefab);
            objects.Add(go);
            return go;
        }

        
        protected GameObject CreateCommonPrefab(string relPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/nadena.dev.modular-avatar/UnitTests/_CommonAssets/" + relPath);

            var go = Object.Instantiate(prefab);
            objects.Add(go);
            return go;
        }

        protected T LoadAsset<T>(string relPath) where T : UnityEngine.Object
        {
            return ITestSupport.Instance.LoadAsset<T>(GetType(), relPath);
        }

#if MA_VRCSDK3_AVATARS
        protected static AnimationClip findFxClip(GameObject prefab, string layerName)
        {
            var motion = findFxMotion(prefab, layerName) as AnimationClip;
            Assert.NotNull(motion);
            return motion;
        }

        protected static Motion findFxMotion(GameObject prefab, string layerName)
        {
            var layer = findFxLayer(prefab, layerName);
            var state = layer.stateMachine.states[0].state;
            Assert.NotNull(state);

            return state.motion;
        }
#endif

        protected static AnimatorState FindStateInLayer(AnimatorControllerLayer layer, string stateName)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (state.state.name == stateName) return state.state;
            }

            return null;
        }

#if MA_VRCSDK3_AVATARS
        protected static AnimatorControllerLayer findFxLayer(GameObject prefab, string layerName)
        {
            var fx = FindFxController(prefab);

            Assert.NotNull(fx);
            var ac = fx.animatorController as AnimatorController;
            Assert.NotNull(ac);
            Assert.False(fx.isDefault);

            var layer = ac.layers.FirstOrDefault(l => l.name == layerName);
            Assert.NotNull(layer);
            return layer;
        }

        internal static VRCAvatarDescriptor.CustomAnimLayer FindFxController(GameObject prefab)
        {
            return FindController(prefab, VRCAvatarDescriptor.AnimLayerType.FX);
        }
        
        internal static VRCAvatarDescriptor.CustomAnimLayer FindController(GameObject prefab, VRCAvatarDescriptor.AnimLayerType layerType)
        {
            return prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .FirstOrDefault(l => l.type == layerType);
        }
        
        protected int FindFxLayerIndex(GameObject prefab, AnimatorControllerLayer layer)
        {
            var fx = (AnimatorController)FindFxController(prefab).animatorController;
            return fx.layers.TakeWhile(l => l.stateMachine != layer.stateMachine).Count();
        }
#endif
    }
}