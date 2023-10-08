using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class RetargetMeshesTest : TestBase
    {
        // Real world case of this test case is with skinned mesh without bones or skinned mesh renderer with null mesh. 
        [Test]
        public void RootBoneOnly()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");

            var skinnedMeshRenderer = root.AddComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.sharedMesh = new Mesh();
            skinnedMeshRenderer.rootBone = b.transform;
            Debug.Assert(skinnedMeshRenderer.bones.Length == 0);

            var build_context =
                new nadena.dev.ndmf.BuildContext(root.GetComponent<VRCAvatarDescriptor>(), null);
            var torc = new AnimationServicesContext();
            torc.OnActivate(build_context);

            var bonedb = new BoneDatabase();
            bonedb.AddMergedBone(b.transform);

            new RetargetMeshes().OnPreprocessAvatar(root, bonedb, torc.PathMappings);

            Assert.AreEqual(a.transform, skinnedMeshRenderer.rootBone);
        }

        [Test]
        public void NoMeshRootBoneOnly()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            b.transform.localScale = new Vector3(2, 2, 2);

            var skinnedMeshRenderer = root.AddComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.sharedMesh = null;
            skinnedMeshRenderer.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            skinnedMeshRenderer.rootBone = b.transform;
            Debug.Assert(skinnedMeshRenderer.bones.Length == 0);

            var build_context =
                new nadena.dev.ndmf.BuildContext(root.GetComponent<VRCAvatarDescriptor>(), null);
            var torc = new AnimationServicesContext();
            torc.OnActivate(build_context);

            var bonedb = new BoneDatabase();
            bonedb.AddMergedBone(b.transform);

            new RetargetMeshes().OnPreprocessAvatar(root, bonedb, torc.PathMappings);

            Assert.AreEqual(a.transform, skinnedMeshRenderer.rootBone);
            Assert.AreEqual(new Bounds(new Vector3(0, 0, 0), new Vector3(2, 2, 2)),
                skinnedMeshRenderer.localBounds);
        }
    }
}