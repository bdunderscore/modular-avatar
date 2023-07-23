using System;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using NUnit.Framework;
using UnityEditor.VersionControl;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.ReplaceObject
{
    public class ReplaceObjectTests : TestBase
    {
        void Process(GameObject root)
        {
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            new ReplaceObjectPass(new BuildContext(avDesc)).Process();
        }

        [Test]
        public void smokeTest()
        {
            var root = CreateRoot("root");
            var replacee = CreateChild(root, "replacee");
            var replacement = CreateChild(root, "replacement");

            replacee.AddComponent<SphereCollider>();
            replacement.AddComponent<BoxCollider>();

            var replaceObject = root.AddComponent<ModularAvatarReplaceObject>();
            replaceObject.targetObject.referencePath = RuntimeUtil.AvatarRootPath(replacee);

            Process(root);

            Assert.AreEqual(1, root.transform.childCount);
            Assert.AreEqual(root.transform.GetChild(0).gameObject, replacement);
            //Assert.AreEqual(replacement.name, "replacee");
            Assert.IsTrue(replacee == null);

            Assert.NotNull(replacement.GetComponent<BoxCollider>());
            Assert.IsNull(root.GetComponentInChildren<SphereCollider>());
        }

        public class TestComponent : MonoBehaviour
        {
            public Transform transformRef;
            public GameObject gameObjectRef;
            public BoxCollider memberRef;
            public BoxCollider secondMemberRef;
            public SphereCollider lostRef;
            public Transform rootRef;
        }

        [Test]
        public void rewritesReferences()
        {
            var root = CreateRoot("root");
            var replacee = CreateChild(root, "replacee");
            var replacement = CreateChild(root, "replacement");
            var reference = CreateChild(root, "reference");

            var testComponent = reference.AddComponent<TestComponent>();
            testComponent.transformRef = replacee.transform;
            testComponent.gameObjectRef = replacee;
            testComponent.memberRef = replacee.AddComponent<BoxCollider>();
            testComponent.secondMemberRef = replacee.AddComponent<BoxCollider>();
            testComponent.lostRef = replacee.AddComponent<SphereCollider>();
            testComponent.rootRef = root.transform;

            var newBoxCollider1 = replacement.AddComponent<BoxCollider>();
            var newBoxCollider2 = replacement.AddComponent<BoxCollider>();
            newBoxCollider1.center = Vector3.up; // just to make it easier to observe in the debugger
            newBoxCollider2.center = Vector3.up * 2;

            var replaceObject = replacement.AddComponent<ModularAvatarReplaceObject>();
            replaceObject.targetObject.Set(replacee);

            Process(root);

            Assert.AreEqual(2, root.transform.childCount);
            Assert.AreEqual(root.transform.GetChild(0).gameObject, replacement);
            Assert.AreEqual(testComponent.transformRef, replacement.transform);
            Assert.AreEqual(testComponent.gameObjectRef, replacement);
            Assert.AreEqual(testComponent.memberRef, newBoxCollider1);
            Assert.AreEqual(testComponent.secondMemberRef, newBoxCollider2);
            Assert.AreEqual(testComponent.lostRef, null);
            Assert.AreEqual(testComponent.rootRef, root.transform);
        }

        // Test: disallow replacing parent of replacee
        [Test]
        public void disallowReplacingParent()
        {
            var root = CreateRoot("root");
            var replacee = CreateChild(root, "replacee");
            var replacement = CreateChild(root, "replacement");

            var replaceObject = replacement.AddComponent<ModularAvatarReplaceObject>();
            replaceObject.targetObject.Set(root);

            BuildReport.Clear();
            Assert.Throws<Exception>(() =>
            {
                using (BuildReport.CurrentReport.ReportingOnAvatar(root.GetComponent<VRCAvatarDescriptor>()))
                {
                    Process(root);
                }
            });
        }

        [Test]
        public void abortWhenTargetNull()
        {
            var root = CreateRoot("root");
            var replacee = CreateChild(root, "replacee");
            var replacement = CreateChild(root, "replacement");

            var replaceObject = replacement.AddComponent<ModularAvatarReplaceObject>();
            replaceObject.targetObject.Set(null);

            BuildReport.Clear();
            Assert.Throws<Exception>(() =>
            {
                using (BuildReport.CurrentReport.ReportingOnAvatar(root.GetComponent<VRCAvatarDescriptor>()))
                {
                    Process(root);
                }
            });
        }

        // Test: child object handling
        [Test]
        public void preservesExistingChildObjects()
        {
            var root = CreateRoot("root");
            var replacee = CreateChild(root, "replacee");
            var replacement = CreateChild(root, "replacement");

            var child1 = CreateChild(replacee, "child1");
            var child2 = CreateChild(replacement, "child2");

            var replaceObject = replacement.AddComponent<ModularAvatarReplaceObject>();
            replaceObject.targetObject.Set(replacee);

            Process(root);

            Assert.AreEqual(1, root.transform.childCount);
            Assert.AreEqual(replacement.transform, child1.transform.parent);
            Assert.AreEqual(replacement.transform, child2.transform.parent);
        }

        // Test: PathMappings
        [Test]
        public void updatesPathMappings()
        {
            var root = CreateRoot("root");
            var replacee = CreateChild(root, "replacee");
            var replacement = CreateChild(root, "replacement");

            var replaceObject = replacement.AddComponent<ModularAvatarReplaceObject>();
            replaceObject.targetObject.Set(replacee);

            PathMappings.Init(root);
            Process(root);

            Assert.AreEqual("replacement", PathMappings.MapPath("replacee"));
        }
    }
}