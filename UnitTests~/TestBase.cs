using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime.components;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace modular_avatar_tests
{
    public class TestBase
    {
        internal static bool[] RunFilterPrimitives(IMeshSelector filter, Renderer renderer, Mesh mesh, int submesh = 0)
        {
            using var selectorJob = new MeshSelectorJob(renderer, mesh);
            var desc = selectorJob.MeshData.GetSubMesh(submesh);
            int vertsPerPrim = desc.topology switch
            {
                MeshTopology.Triangles => 3,
                MeshTopology.Quads => 4,
                _ => 1
            };

            using var primMask = new NativeArray<bool>(desc.indexCount / vertsPerPrim, Allocator.TempJob);
            filter.MarkFilteredPrimitives(selectorJob, submesh, primMask).Complete();
            return primMask.ToArray();
        }

        protected Mesh CreateShapeFilterTestMesh()
        {
            var vertices = new Vector3[15];
            var triangles = new int[vertices.Length];
            for (int p = 0; p < 5; p++)
            {
                var baseIndex = p * 3;
                vertices[baseIndex] = new Vector3(p * 2, 0, 0);
                vertices[baseIndex + 1] = new Vector3(p * 2 + 1, 0, 0);
                vertices[baseIndex + 2] = new Vector3(p * 2, 1, 0);
                triangles[baseIndex] = baseIndex;
                triangles[baseIndex + 1] = baseIndex + 1;
                triangles[baseIndex + 2] = baseIndex + 2;
            }

            var mesh = TrackObject(new Mesh
            {
                vertices = vertices,
                triangles = triangles
            });
            // Primitives: Positive, Negative, Center, Positive+Negative, static.
            AddBlendShape(mesh, "Positive", 0, 9);
            AddBlendShape(mesh, "Negative", 3, 10);
            AddBlendShape(mesh, "Center", 6);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBlendShape(Mesh mesh, string name, params int[] movedVertices)
        {
            var deltas = new Vector3[mesh.vertexCount];
            foreach (var vertex in movedVertices) deltas[vertex] = Vector3.up;
            mesh.AddBlendShapeFrame(name, 100, deltas, new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount]);
        }

        private const string TEMP_ASSET_PATH = "Assets/ZZZ_Temp";

        private List<UnityEngine.Object> objects;

        public T TrackObject<T>(T obj) where T : UnityEngine.Object
        {
            objects.Add(obj);
            return obj;
        }
        
        [SetUp]
        public virtual void Setup()
        {
            ITestSupport.Instance.Setup();

            objects = new List<Object>();
        }

        [TearDown]
        public virtual void Teardown()
        {
            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            AssetDatabase.DeleteAsset(TEMP_ASSET_PATH);
            FileUtil.DeleteFileOrDirectory(TEMP_ASSET_PATH);
            
            ITestSupport.Instance.Teardown();
        }

        protected nadena.dev.ndmf.BuildContext CreateContext(GameObject root, [CanBeNull] string platformName = null)
        {
            platformName ??= AmbientPlatform.DefaultPlatform.QualifiedName;
            var platform = PlatformRegistry.PlatformProviders[platformName];
            return new nadena.dev.ndmf.BuildContext(root, TEMP_ASSET_PATH, platform); // TODO - cleanup
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

        public void AddMinimalAvatarComponents(GameObject obj)
        {
            obj.GetOrAddComponent<Animator>();
#if MA_VRCSDK3_AVATARS
            obj.GetOrAddComponent<VRCAvatarDescriptor>();
#else
            obj.GetOrAddComponent<NDMFAvatarRoot>();
#endif
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
