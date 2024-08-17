using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

namespace ShapeChangerTests
{
    public class TargetRenderer : TestBase
    {
        [Test]
        public void SetTargetRenderers()
        {
            var root = CreatePrefab("TargetRenderer.prefab");
            AvatarProcessor.ProcessAvatar(root);

            var smrA = root.transform.Find("Mesh_A").GetComponent<SkinnedMeshRenderer>();
            var smrB = root.transform.Find("Mesh_B").GetComponent<SkinnedMeshRenderer>();
            var sharedMeshA = smrA.sharedMesh;
            var sharedMeshB = smrB.sharedMesh;

            Assert.AreEqual(100.0f, smrA.GetBlendShapeWeight(sharedMeshA.GetBlendShapeIndex("key1")), 0.1f);
            Assert.AreEqual(0.0f, smrB.GetBlendShapeWeight(sharedMeshB.GetBlendShapeIndex("key1")), 0.1f);

            Assert.AreEqual(0.0f, smrA.GetBlendShapeWeight(sharedMeshA.GetBlendShapeIndex("key2")), 0.1f);
            Assert.AreEqual(100.0f, smrB.GetBlendShapeWeight(sharedMeshB.GetBlendShapeIndex("key2")), 0.1f);

            Assert.AreEqual(100.0f, smrA.GetBlendShapeWeight(sharedMeshA.GetBlendShapeIndex("key3")), 0.1f);
            Assert.AreEqual(0.0f, smrB.GetBlendShapeWeight(sharedMeshB.GetBlendShapeIndex("key3")), 0.1f);
        }
    }
}