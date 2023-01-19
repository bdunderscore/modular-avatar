using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using NUnit.Framework.Internal;
using UnityEngine;

namespace modular_avatar_tests
{
    public class BoneProxyTest : TestBase
    {
        [Test]
        public void TestBoneProxy()
        {
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildAtRoot, expectSnapPos: true, expectSnapRot: true);
            AssertAttachmentMode(BoneProxyAttachmentMode.Unset, expectSnapPos: true, expectSnapRot: true);
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildKeepPosition, expectSnapPos: false,
                expectSnapRot: true);
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildKeepRotation, expectSnapPos: true,
                expectSnapRot: false);
            AssertAttachmentMode(BoneProxyAttachmentMode.AsChildKeepWorldPose, expectSnapPos: false,
                expectSnapRot: false);
        }

        [Test]
        public void TestNonHumanoidTarget()
        {
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var reference = CreateChild(root, "ref");

            var boneProxy = reference.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = root.transform;
            boneProxy.ClearCache();
            Assert.AreEqual(root.transform, boneProxy.target);

            boneProxy.target = target.transform;
            boneProxy.ClearCache();
            Assert.AreEqual(target.transform, boneProxy.target);

            target.name = "target2";
            boneProxy.ClearCache();
            Assert.IsNull(boneProxy.target);
        }

        private void AssertAttachmentMode(BoneProxyAttachmentMode attachmentMode, bool expectSnapPos,
            bool expectSnapRot)
        {
            AssertAttachmentModeAtBuild(attachmentMode, expectSnapPos, expectSnapRot);
            AssertAttachmentModeInEditor(attachmentMode, expectSnapPos, expectSnapRot);
        }

        private void AssertAttachmentModeInEditor(BoneProxyAttachmentMode attachmentMode, bool expectSnapPos,
            bool expectSnapRot)
        {
            // Unset gets converted in the custom inspector; until it is, we don't snap (since we need to know the
            // position to heuristically set the snapping mode).
            if (attachmentMode == BoneProxyAttachmentMode.Unset) return;

            var root = CreateRoot("root");
            var bone = CreateChild(root, "bone");
            var proxy = CreateChild(root, "proxy");

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = bone.transform;
            boneProxy.attachmentMode = attachmentMode;

            bone.transform.localPosition = Vector3.one;
            bone.transform.localRotation = Quaternion.Euler(123, 45, 6);

            boneProxy.Update();

            if (expectSnapPos)
            {
                Assert.LessOrEqual(Vector3.Distance(proxy.transform.position, bone.transform.position), 0.0001f);
            }
            else
            {
                Assert.GreaterOrEqual(Vector3.Distance(proxy.transform.position, bone.transform.position), 0.0001f);
            }

            if (expectSnapRot)
            {
                Assert.LessOrEqual(Quaternion.Angle(proxy.transform.rotation, bone.transform.rotation), 0.0001f);
            }
            else
            {
                Assert.GreaterOrEqual(Quaternion.Angle(proxy.transform.rotation, bone.transform.rotation), 0.0001f);
            }
        }

        private void AssertAttachmentModeAtBuild(BoneProxyAttachmentMode attachmentMode, bool expectSnapPos,
            bool expectSnapRot)
        {
            var root = CreateRoot("root");
            var bone = CreateChild(root, "bone");
            var proxy = CreateChild(root, "proxy");

            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = bone.transform;
            boneProxy.attachmentMode = attachmentMode;

            bone.transform.localPosition = Vector3.one;
            bone.transform.localRotation = Quaternion.Euler(123, 45, 6);

            AvatarProcessor.ProcessAvatar(root);

            Assert.AreEqual(proxy.transform.parent, bone.transform);

            if (expectSnapPos)
            {
                Assert.LessOrEqual(Vector3.Distance(proxy.transform.localPosition, Vector3.zero), 0.0001f);
            }
            else
            {
                Assert.LessOrEqual(Vector3.Distance(proxy.transform.position, Vector3.zero), 0.0001f);
            }

            if (expectSnapRot)
            {
                Assert.LessOrEqual(Quaternion.Angle(proxy.transform.localRotation, Quaternion.identity), 0.0001f);
            }
            else
            {
                Assert.LessOrEqual(Quaternion.Angle(proxy.transform.rotation, Quaternion.identity), 0.0001f);
            }
        }
    }
}