using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class TestBase
    {
        private List<GameObject> objects;

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
        }

        [OneTimeTearDown]
        void Cleanup()
        {
            Util.DeleteTemporaryAssets();
        }

        protected GameObject CreateRoot(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            // Needed for avatar path finding functions to work properly
            go.AddComponent(typeof(VRCAvatarDescriptor));
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
    }
}