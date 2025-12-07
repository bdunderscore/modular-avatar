#if MA_VRCSDK3_AVATARS

using System.Collections.Immutable;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.animation;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;

namespace modular_avatar_tests
{
    public class BlendshapeSyncIntegrationTest : TestBase
    {
        [Test]
        public void IntegrationTest_BlendshapeSync()
        {
            var root = CreatePrefab("BlendshapeSyncIntegrationTest.prefab");

            AvatarProcessor.ProcessAvatar(root);

            var clip = findFxClip(root, "Base Layer");

            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Select(binding =>
                {
                    var constantKey = AnimationUtility.GetEditorCurve(clip, binding).keys[0].value;
                    return (binding.path, binding.propertyName, constantKey);
                }).ToImmutableHashSet();

            Assert.True(bindings.Contains(("BaseMesh", "blendShape.shape_0", 0.1f)));
            Assert.True(bindings.Contains(("BaseMesh", "blendShape.shape_0_local", 0.3f)));
            Assert.True(bindings.Contains(("BaseMesh", "blendShape.shape_1", 0.2f)));
            Assert.True(bindings.Contains(("BaseMesh", "blendShape.missing_mesh_shape", 0.4f)));
            Assert.True(bindings.Contains(("BaseMesh", "blendShape.missing_mesh_shape_2", 0.5f)));

            Assert.True(bindings.Contains(("SyncedMesh", "blendShape.shape_0_local", 0.1f)));
            Assert.True(bindings.Contains(("SyncedMesh", "blendShape.shape_1", 0.2f)));

            Assert.AreEqual(bindings.Count, 7);
        }

        [Test]
        public void BlendshapeSync_CopiesValueToDownstreamMeshes()
        {
            var root = CreatePrefab("BlendshapeSyncIntegrationTest.prefab");

            // Set a value on BaseMesh's shape_0 and shape_1
            var baseMesh = root.transform.Find("BaseMesh").GetComponent<UnityEngine.SkinnedMeshRenderer>();
            var syncedMesh = root.transform.Find("SyncedMesh").GetComponent<UnityEngine.SkinnedMeshRenderer>();
            int shape0 = baseMesh.sharedMesh.GetBlendShapeIndex("shape_0");
            int shape1 = baseMesh.sharedMesh.GetBlendShapeIndex("shape_1");
            
            // If there's an animator controller on the root animator, it'll apply the initial values in that animation,
            // which breaks this test. So clear it here.
            var animator = root.GetComponent<UnityEngine.Animator>();
            animator.runtimeAnimatorController = null;
            
            // Set test values
            baseMesh.SetBlendShapeWeight(shape0, 42.5f);
            baseMesh.SetBlendShapeWeight(shape1, 13.7f);

            // Process the avatar (should sync values)
            AvatarProcessor.ProcessAvatar(root);
            
            // Assert that base mesh blendshape weights are unchanged
            Assert.AreEqual(42.5f, baseMesh.GetBlendShapeWeight(shape0));
            Assert.AreEqual(13.7f, baseMesh.GetBlendShapeWeight(shape1));

            // By default, the prefab syncs:
            //   BaseMesh.shape_0 -> SyncedMesh.shape_0_local
            //   BaseMesh.shape_1 -> SyncedMesh.shape_1
            int syncedShape0Local = syncedMesh.sharedMesh.GetBlendShapeIndex("shape_0_local");
            int syncedShape1 = syncedMesh.sharedMesh.GetBlendShapeIndex("shape_1");
            Assert.AreEqual(42.5f, syncedMesh.GetBlendShapeWeight(syncedShape0Local));
            Assert.AreEqual(13.7f, syncedMesh.GetBlendShapeWeight(syncedShape1));
        }
    }
}

#endif