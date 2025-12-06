using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    internal class MeshSettingsTests : TestBase
    {
        private static readonly string PREFAB_NAME = "MeshSettingsTests.prefab";

        [Test]
        public void TestProbeAnchor()
        {
            var prefab = CreatePrefab(PREFAB_NAME);
            var context = new BuildContext(prefab);
            new MeshSettingsPass(context).OnPreprocessAvatar();

            var root = prefab.transform.Find("RendererRoot");
            var target = prefab.transform.Find("ProbeTarget");
            var obj1 = prefab.transform.Find("ProbeTargetRenderers/SkinnedMeshRenderer").GetComponent<Renderer>();
            var obj2 = prefab.transform.Find("ProbeTargetRenderers/MeshRenderer").GetComponent<Renderer>();
            var obj3 = prefab.transform.Find("ProbeTargetRenderers/ParticleSystemRenderer").GetComponent<Renderer>();
            var obj4 = prefab.transform.Find("ProbeTargetRenderers/TrailRenderer").GetComponent<Renderer>();
            var obj5 = prefab.transform.Find("ProbeTargetRenderers/SetOrInherit").GetComponent<Renderer>();

            Assert.AreEqual(target, obj1.probeAnchor);
            Assert.AreEqual(target, obj2.probeAnchor);
            Assert.AreEqual(target, obj3.probeAnchor);
            Assert.AreEqual(target, obj4.probeAnchor);
            Assert.AreEqual(target, obj5.probeAnchor);
        }

        [Test]
        public void TestProbeAnchorOverrides()
        {
            var prefab = CreatePrefab(PREFAB_NAME);
            var context = new BuildContext(prefab);
            new MeshSettingsPass(context).OnPreprocessAvatar();

            var noninherit = prefab.transform.Find("ProbeTargetRenderers/NonInherited").GetComponent<MeshRenderer>();
            var overrideset = prefab.transform.Find("ProbeTargetRenderers/OverrideSet").GetComponent<MeshRenderer>();
            var setOrInherit = prefab.transform.Find("ProbeTargetRenderers SetOrInherit No Parent").GetComponent<MeshRenderer>();

            Assert.AreEqual(noninherit.transform.Find("Target"), noninherit.probeAnchor);
            Assert.AreEqual(overrideset.transform.Find("Target"), overrideset.probeAnchor);
            Assert.AreEqual(setOrInherit.transform.Find("Target"), setOrInherit.probeAnchor);
        }

        [Test]
        public void TestSetBounds()
        {
            var prefab = CreatePrefab(PREFAB_NAME);
            var context = new BuildContext(prefab);
            new MeshSettingsPass(context).OnPreprocessAvatar();

            var target = prefab.transform.Find("ProbeTarget");

            var inherit = prefab.transform.Find("Bounds/Inherit").GetComponent<SkinnedMeshRenderer>();
            var overridden = prefab.transform.Find("Bounds/Overridden").GetComponent<SkinnedMeshRenderer>();
            var notset = prefab.transform.Find("Bounds/NotSet").GetComponent<SkinnedMeshRenderer>();

            Assert.AreEqual(new Vector3(1, 2, 3), inherit.bounds.center);
            Assert.AreEqual(new Vector3(4, 5, 6), inherit.bounds.extents);
            Assert.AreEqual(target, inherit.rootBone);

            Assert.AreEqual(new Vector3(8, 8, 8), overridden.bounds.center);
            Assert.AreEqual(new Vector3(9, 9, 9), overridden.bounds.extents);
            Assert.AreEqual(overridden.transform.Find("Target"), overridden.rootBone);

            Assert.AreEqual(new Vector3(0, 0, 0), notset.bounds.center);
            Assert.AreEqual(new Vector3(2, 2, 2), notset.bounds.extents);
            Assert.AreEqual(notset.transform.Find("Target"), notset.rootBone);
        }
    }
}