using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;

namespace modular_avatar_tests
{
    public class MoveToTest : TestBase
    {
        [Test]
        public void HasExpectedDefaults()
        {
            var moveTo = CreateRoot("root").AddComponent<ModularAvatarMoveTo>();

            Assert.That(moveTo.target, Is.Not.Null);
            Assert.That(moveTo.matchPosition, Is.True);
            Assert.That(moveTo.matchRotation, Is.True);
            Assert.That(moveTo.matchScale, Is.False);
        }

        [Test]
        public void MatchesEnabledPropertiesInEditor()
        {
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var source = CreateChild(root, "source");

            target.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30));
            target.transform.localScale = new Vector3(2, 3, 4);
            source.transform.SetPositionAndRotation(new Vector3(4, 5, 6), Quaternion.Euler(40, 50, 60));
            source.transform.localScale = Vector3.one;

            var moveTo = source.AddComponent<ModularAvatarMoveTo>();
            moveTo.target.Set(target);
            moveTo.MatchTarget();

            Assert.LessOrEqual(Vector3.Distance(source.transform.position, target.transform.position), 0.0001f);
            Assert.LessOrEqual(Quaternion.Angle(source.transform.rotation, target.transform.rotation), 0.0001f);
            Assert.LessOrEqual(Vector3.Distance(source.transform.localScale, Vector3.one), 0.0001f);
        }

        [Test]
        public void BuildMatchesEnabledPropertiesAndDestroysComponent()
        {
            var root = CreateRoot("root");
            var target = CreateChild(root, "target");
            var source = CreateChild(root, "source");

            target.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30));
            target.transform.localScale = new Vector3(2, 3, 4);
            source.transform.SetPositionAndRotation(new Vector3(4, 5, 6), Quaternion.Euler(40, 50, 60));
            source.transform.localScale = new Vector3(5, 6, 7);

            var moveTo = source.AddComponent<ModularAvatarMoveTo>();
            moveTo.target.Set(target);
            moveTo.matchPosition = false;
            moveTo.matchRotation = true;
            moveTo.matchScale = true;

            new MoveToPluginPass().ExecuteForTesting(CreateContext(root));

            Assert.That(source.GetComponent<ModularAvatarMoveTo>(), Is.Null);
            Assert.LessOrEqual(Vector3.Distance(source.transform.position, new Vector3(4, 5, 6)), 0.0001f);
            Assert.LessOrEqual(Quaternion.Angle(source.transform.rotation, target.transform.rotation), 0.0001f);
            Assert.LessOrEqual(Vector3.Distance(source.transform.localScale, target.transform.localScale), 0.0001f);
        }

        [Test]
        public void MatchScaleAccountsForDifferentParentScales()
        {
            var root = CreateRoot("root");
            var targetParent = CreateChild(root, "target parent");
            targetParent.transform.localScale = new Vector3(2, 3, 4);
            var target = CreateChild(targetParent, "target");
            target.transform.localScale = new Vector3(5, 6, 7);

            var sourceParent = CreateChild(root, "source parent");
            sourceParent.transform.localScale = new Vector3(3, 2, 1);
            var source = CreateChild(sourceParent, "source");

            var moveTo = source.AddComponent<ModularAvatarMoveTo>();
            moveTo.target.Set(target);
            moveTo.matchScale = true;
            moveTo.MatchTarget();

            Assert.LessOrEqual(Vector3.Distance(source.transform.lossyScale, target.transform.lossyScale), 0.0001f);
        }

        [Test]
        public void BuildRunsAfterBoneProxy()
        {
            var root = CreateRoot("root");
            var destination = CreateChild(root, "destination");
            destination.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30));
            destination.AddComponent<MeshRenderer>();

            var proxy = CreateChild(root, "proxy");
            proxy.AddComponent<MeshRenderer>();
            var boneProxy = proxy.AddComponent<ModularAvatarBoneProxy>();
            boneProxy.target = destination.transform;
            boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;

            var source = CreateChild(root, "source");
            source.AddComponent<MeshRenderer>();
            var moveTo = source.AddComponent<ModularAvatarMoveTo>();
            moveTo.target.Set(proxy);

            nadena.dev.modular_avatar.core.editor.AvatarProcessor.ProcessAvatar(root);

            Assert.That(source.GetComponent<ModularAvatarMoveTo>(), Is.Null);
            Assert.LessOrEqual(Vector3.Distance(source.transform.position, proxy.transform.position), 0.0001f);
            Assert.LessOrEqual(Quaternion.Angle(source.transform.rotation, proxy.transform.rotation), 0.0001f);
        }
    }
}
