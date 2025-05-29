#if MA_VRCSDK3_AVATARS

#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NUnit.Framework;

using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using VRC.SDK3.Dynamics.Contact.Components;

#endregion

namespace modular_avatar_tests.RenameCollisionTags
{
    public class RenameCollisionTagsTests : TestBase
    {
        [Test]
        public void RenameCollisionTagsForReceiver()
        {
            var avatar = CreateRoot("avatar");

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact.collisionTags[0], Does.StartWith("TagA$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["TagA$".Length..], out _));
        }

        // Because `ContactBase` is an abstract class, we need to test both `VRCContactSender` and `VRCContactReceiver`.
        [Test]
        public void RenameCollisionTagsForSender()
        {
            var avatar = CreateRoot("avatar");

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contact = avatar.AddComponent<VRCContactSender>();
            contact.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact.collisionTags[0], Does.StartWith("TagA$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["TagA$".Length..], out _));
        }

        [Test]
        public void AllowEmptyTagString()
        {
            var avatar = CreateRoot("avatar");
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact.collisionTags[0], Does.StartWith("$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["$".Length..], out _));
        }

        [Test]
        public void DoNotRenameUnspecifiedTags()
        {
            var avatar = CreateRoot("avatar");

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagB" };

            RenameCollisionTagsPass.Process(avatar);
            Assert.That(contact.collisionTags[0], Is.EqualTo("TagB"));
        }

        [Test]
        public void PreserveTagOrderAfterRename()
        {
            var avatar = CreateRoot("avatar");

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagB" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA", "TagB", "TagC" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact.collisionTags[0], Is.EqualTo("TagA"));
            Assert.That(contact.collisionTags[1], Does.StartWith("TagB$"));
            Assert.That(contact.collisionTags[2], Is.EqualTo("TagC"));
        }

        [Test]
        public void RenameSameTagsConsistently()
        {
            var avatar = CreateRoot("avatar");

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contact1 = avatar.AddComponent<VRCContactReceiver>();
            contact1.collisionTags = new List<string> { "TagA" };

            var contact2 = avatar.AddComponent<VRCContactReceiver>();
            contact2.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact1.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact1.collisionTags[0], Is.EqualTo(contact2.collisionTags[0]));
        }

        [Test]
        public void RenameSameTagsForSenderAndReceiverConsistently()
        {
            var avatar = CreateRoot("avatar");
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var sender = avatar.AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { "TagA" };

            var receiver = avatar.AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(sender.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(receiver.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(sender.collisionTags[0], Is.EqualTo(receiver.collisionTags[0]));
        }

        [Test]
        public void RenameMultipleTagsIndividually()
        {
            var avatar = CreateRoot("avatar");

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" },
                new() { name = "TagB" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA", "TagB" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact.collisionTags[0], Is.Not.EqualTo(contact.collisionTags[1]));
        }

        [Test]
        public void RenameSameTagsForChildrenConsistently()
        {
            var avatar = CreateRoot("avatar");
            var child1 = new GameObject("child1");
            child1.transform.parent = avatar.transform;
            var child2 = new GameObject("child2");
            child2.transform.parent = avatar.transform;

            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contact1 = child1.AddComponent<VRCContactReceiver>();
            contact1.collisionTags = new List<string> { "TagA" };

            var contact2 = child2.AddComponent<VRCContactReceiver>();
            contact2.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact1.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact2.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact1.collisionTags[0], Is.EqualTo(contact2.collisionTags[0]));
        }

        [Test]
        public void PreferChildRenameCollisionTagsOverParent()
        {
            var avatar = CreateRoot("avatar");
            var child = new GameObject("child");
            child.transform.parent = avatar.transform;

            var renameCollisionTagsParent = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameCollisionTagsParent.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };
            var renameCollisionTagsChild = child.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameCollisionTagsChild.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contactA = avatar.AddComponent<VRCContactReceiver>();
            contactA.collisionTags = new List<string> { "TagA" };

            var contactB = child.AddComponent<VRCContactReceiver>();
            contactB.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contactA.collisionTags[0], Is.Not.EqualTo(contactB.collisionTags[0]));
        }

        [Test]
        public void RenameTagsInDeepHierarchyConsistently()
        {
            var avatar = CreateRoot("avatar");
            var child = new GameObject("child");
            child.transform.parent = avatar.transform;
            var grandchild = new GameObject("grandchild");
            grandchild.transform.parent = child.transform;

            var renameCollisionTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameCollisionTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contactRoot = avatar.AddComponent<VRCContactReceiver>();
            contactRoot.collisionTags = new List<string> { "TagA" };
            var contactChild = child.AddComponent<VRCContactReceiver>();
            contactChild.collisionTags = new List<string> { "TagA" };
            var contactGrandchild = grandchild.AddComponent<VRCContactReceiver>();
            contactGrandchild.collisionTags = new List<string> { "TagA" };

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contactRoot.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contactRoot.collisionTags[0], Is.EqualTo(contactChild.collisionTags[0]));
            Assert.That(contactRoot.collisionTags[0], Is.EqualTo(contactGrandchild.collisionTags[0]));
        }

        [Test]
        public void KeepCollisionTagsListEmpty()
        {
            var avatar = CreateRoot("avatar");
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string>();

            RenameCollisionTagsPass.Process(avatar);

            Assert.That(contact.collisionTags, Is.Empty);
        }
    }
}

#endif
