using System;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    /// <summary>
    /// Regression test: When the retargeted root bone has a zero-scale axis, the bounds extents computation
    /// divided by zero, producing Inf/NaN and corrupting localBounds.
    /// </summary>
    public class MeshRetargeterBoundsTest : TestBase
    {
        /// <summary>
        /// When the new root bone has a zero lossy scale on one axis, the bounds extents
        /// should not become NaN or Inf. The division by zero should be guarded.
        /// </summary>
        [Test]
        public void RetargetBones_ZeroScaleBone_DoesNotProduceNaNBounds()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);

            // Create bone hierarchy: root > parentBone > childBone
            var parentBone = CreateChild(root, "parentBone");
            var childBone = CreateChild(parentBone, "childBone");

            // Set parentBone to have zero Y scale — this will be the newRootBone
            parentBone.transform.localScale = new Vector3(1, 0, 1);

            // Create SkinnedMeshRenderer with childBone as rootBone
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            mesh.name = "TestMesh";
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            renderer.sharedMesh = mesh;
            renderer.localBounds = mesh.bounds;
            renderer.rootBone = childBone.transform;

            // Set up bone database: mark childBone as retargetable
            // GetRetargetedBone(childBone) will return parentBone (the first non-retargetable ancestor)
            var boneDatabase = new BoneDatabase();
            boneDatabase.AddMergedBone(childBone.transform);

            var buildContext = new nadena.dev.ndmf.BuildContext(root, null);
            var asc = buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            new RetargetMeshes().OnPreprocessAvatar(root, boneDatabase, asc);

            // After retargeting, verify bounds are valid (not NaN/Inf)
            var bounds = renderer.localBounds;
            Assert.IsFalse(float.IsNaN(bounds.extents.x), "bounds.extents.x is NaN");
            Assert.IsFalse(float.IsNaN(bounds.extents.y), "bounds.extents.y is NaN");
            Assert.IsFalse(float.IsNaN(bounds.extents.z), "bounds.extents.z is NaN");
            Assert.IsFalse(float.IsInfinity(bounds.extents.x), "bounds.extents.x is Inf");
            Assert.IsFalse(float.IsInfinity(bounds.extents.y), "bounds.extents.y is Inf");
            Assert.IsFalse(float.IsInfinity(bounds.extents.z), "bounds.extents.z is Inf");

            Assert.IsFalse(float.IsNaN(bounds.center.x), "bounds.center.x is NaN");
            Assert.IsFalse(float.IsNaN(bounds.center.y), "bounds.center.y is NaN");
            Assert.IsFalse(float.IsNaN(bounds.center.z), "bounds.center.z is NaN");
            Assert.IsFalse(float.IsInfinity(bounds.center.x), "bounds.center.x is Inf");
            Assert.IsFalse(float.IsInfinity(bounds.center.y), "bounds.center.y is Inf");
            Assert.IsFalse(float.IsInfinity(bounds.center.z), "bounds.center.z is Inf");
        }
    }
}
