using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using NUnit.Framework;
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
        private const string MinimalAvatarGuid = "60d3416d1f6af4a47bf9056aefc38333";

        [SetUp]
        public virtual void Setup()
        {
            if (_scriptToDirectory == null)
            {
                _scriptToDirectory = new Dictionary<System.Type, string>();
                foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new string[] {"Packages/nadena.dev.modular-avatar/UnitTests"}))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var obj = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (obj != null && obj.GetClass() != null)
                    {
                        _scriptToDirectory.Add(obj.GetClass(), Path.GetDirectoryName(path));
                    }
                }
            }

            BuildReport.Clear();
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
        }

        protected nadena.dev.ndmf.BuildContext CreateContext(GameObject root)
        {
            return new nadena.dev.ndmf.BuildContext(root, TEMP_ASSET_PATH); // TODO - cleanup
        }

        protected GameObject CreateRoot(string name)
        {
            var path = AssetDatabase.GUIDToAssetPath(MinimalAvatarGuid);
            var go = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));

            objects.Add(go);
            return go;
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
            var root = _scriptToDirectory[GetType()] + "/";
            var path = root + relPath;

            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.NotNull(obj, "Missing test asset {0}", path);

            return obj;
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
            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);

            Assert.NotNull(fx);
            var ac = fx.animatorController as AnimatorController;
            Assert.NotNull(ac);
            Assert.False(fx.isDefault);

            var layer = ac.layers.FirstOrDefault(l => l.name == layerName);
            Assert.NotNull(layer);
            return layer;
        }
#endif
    }
}