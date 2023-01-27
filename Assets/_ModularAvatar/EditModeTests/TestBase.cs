using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class TestBase
    {
        private List<GameObject> objects;
        private const string MinimalAvatarGuid = "60d3416d1f6af4a47bf9056aefc38333";

        [SetUp]
        public void Setup()
        {
            objects = new List<GameObject>();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var obj in objects)
            {
                Object.DestroyImmediate(obj);
            }

            Util.DeleteTemporaryAssets();
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
            var prefabRoot = "Assets/_ModularAvatar/EditModeTests/" + GetType().Name + "/";
            var prefabPath = prefabRoot + relPath;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab, "Missing test prefab {0}", prefabPath);

            var go = Object.Instantiate(prefab);
            objects.Add(go);
            return go;
        }


        protected static AnimationClip findFxMotion(GameObject prefab, string layerName)
        {
            var layer = findFxLayer(prefab, layerName);
            var state = layer.stateMachine.states[0].state;
            Assert.NotNull(state);

            var motion = state.motion as AnimationClip;
            Assert.NotNull(motion);
            return motion;
        }

        protected static AnimatorState FindStateInLayer(AnimatorControllerLayer layer, string stateName)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (state.state.name == stateName) return state.state;
            }

            return null;
        }

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
    }
}