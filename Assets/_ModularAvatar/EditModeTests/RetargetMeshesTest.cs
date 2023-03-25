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

            BoneDatabase.AddMergedBone(b.transform);
            var context = new BuildContext(root.GetComponent<VRCAvatarDescriptor>());
            new RetargetMeshes().OnPreprocessAvatar(root, context);

            Assert.AreEqual(skinnedMeshRenderer.rootBone, a.transform);
        }
    }
}
