using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace modular_avatar_tests
{
    internal class ProbeAnchorTests : TestBase
    {
        [Test]
        public void TestProbeAnchor()
        {
            var prefab = CreatePrefab("ProbeAnchorTests.prefab");
            AvatarProcessor.ProcessAvatar(prefab);

            var root = prefab.transform.Find("RendererRoot");
            var target = prefab.transform.Find("ProbeTarget");
            var obj1 = prefab.transform.Find("RendererRoot/SkinnedMeshRenderer").GetComponent<Renderer>();
            var obj2 = prefab.transform.Find("RendererRoot/MeshRenderer").GetComponent<Renderer>();
            var obj3 = prefab.transform.Find("RendererRoot/ParticleSystemRenderer").GetComponent<Renderer>();
            var obj4 = prefab.transform.Find("RendererRoot/TrailRenderer").GetComponent<Renderer>();

            Assert.AreEqual(target, obj1.probeAnchor);
            Assert.AreEqual(target, obj2.probeAnchor);
            Assert.AreEqual(target, obj3.probeAnchor);
            Assert.AreEqual(target, obj4.probeAnchor);
        }
    }
}