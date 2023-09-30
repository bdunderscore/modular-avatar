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
    }
}