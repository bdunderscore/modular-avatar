#if MA_VRCSDK3_AVATARS

#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using NUnit.Framework;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;

using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.editor.ErrorReporting;

using BuildContext = nadena.dev.ndmf.BuildContext;
using ErrorReport = nadena.dev.ndmf.ErrorReport;
using SimpleError = nadena.dev.ndmf.SimpleError;

#endregion

namespace nadena.dev.modular_avatar.test.Tests.RenameCollisionTags
{
    public class RenameCollisionTagsTests : TestBase
    {
        [Test]
        public void RenameCollisionTagsForReceiver()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA" };
    
            // Is `RenameCollisionTagsPass().Instance` better?
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Does.StartWith("TagA$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["TagA$".Length..], out _));
        }
    
        // Because `ContactBase` is an abstract class, we need to test both `VRCContactSender` and `VRCContactReceiver`.
        [Test]
        public void RenameCollisionTagsForSender()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactSender>();
            contact.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Does.StartWith("TagA$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["TagA$".Length..], out _));
        }
    
        [Test]
        public void AllowEmptyTagString()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Does.StartWith("$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["$".Length..], out _));
        }
    
        [Test]
        public void DoNotRenameUnspecifiedTags()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagB" };
    
            new RenameCollisionTagsPass().TestExecute(context);
            Assert.That(contact.collisionTags[0], Is.EqualTo("TagB"));
        }
    
        [Test]
        public void PreserveTagOrderAfterRename()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagB", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA", "TagB", "TagC" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Is.EqualTo("TagA"));
            Assert.That(contact.collisionTags[1], Does.StartWith("TagB$"));
            Assert.That(contact.collisionTags[2], Is.EqualTo("TagC"));
        }
    
        [Test]
        public void RenameSameTagsConsistently()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact1 = avatar.AddComponent<VRCContactReceiver>();
            contact1.collisionTags = new List<string> { "TagA" };
    
            var contact2 = avatar.AddComponent<VRCContactReceiver>();
            contact2.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact1.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact1.collisionTags[0], Is.EqualTo(contact2.collisionTags[0]));
        }
    
        [Test]
        public void RenameSameTagsForSenderAndReceiverConsistently()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var sender = avatar.AddComponent<VRCContactSender>();
            sender.collisionTags = new List<string> { "TagA" };
    
            var receiver = avatar.AddComponent<VRCContactReceiver>();
            receiver.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(sender.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(receiver.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(sender.collisionTags[0], Is.EqualTo(receiver.collisionTags[0]));
        }
    
        [Test]
        public void RenameMultipleTagsIndividually()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" },
                new() { name = "TagB", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA", "TagB" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Is.Not.EqualTo(contact.collisionTags[1]));
        }
    
        [Test]
        public void RenameSameTagsForChildrenConsistently()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
            var child1 = new GameObject("child1");
            child1.transform.parent = avatar.transform;
            var child2 = new GameObject("child2");
            child2.transform.parent = avatar.transform;
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact1 = child1.AddComponent<VRCContactReceiver>();
            contact1.collisionTags = new List<string> { "TagA" };
    
            var contact2 = child2.AddComponent<VRCContactReceiver>();
            contact2.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact1.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact2.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact1.collisionTags[0], Is.EqualTo(contact2.collisionTags[0]));
        }
    
        [Test]
        public void PreferClosestRenameCollisionTags()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
            var child = new GameObject("child");
            child.transform.parent = avatar.transform;
    
            var renameCollisionTagsParent = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameCollisionTagsParent.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
            var renameCollisionTagsChild = child.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameCollisionTagsChild.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contactA = avatar.AddComponent<VRCContactReceiver>();
            contactA.collisionTags = new List<string> { "TagA" };
    
            var contactB = child.AddComponent<VRCContactReceiver>();
            contactB.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contactA.collisionTags[0], Is.Not.EqualTo(contactB.collisionTags[0]));
        }
    
        [Test]
        public void RenameTagsWithMultipleParents()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            // Parent1
            var parent1 = new GameObject("Parent1");
            parent1.transform.parent = avatar.transform;
            var renameParent1 = parent1.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameParent1.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            // Parent2
            var parent2 = new GameObject("Parent2");
            parent2.transform.parent = avatar.transform;
            var renameParent2 = parent2.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameParent2.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            // Contact1 under Parent1
            var child1 = new GameObject("Child1");
            child1.transform.parent = parent1.transform;
            var contact1 = child1.AddComponent<VRCContactReceiver>();
            contact1.collisionTags = new List<string> { "TagA" };
    
            // Contact2 under Parent1
            var child2 = new GameObject("Child2");
            child2.transform.parent = parent1.transform;
            var contact2 = child2.AddComponent<VRCContactReceiver>();
            contact2.collisionTags = new List<string> { "TagA" };
    
            // Contact3 under Parent2
            var child3 = new GameObject("Child3");
            child3.transform.parent = parent2.transform;
            var contact3 = child3.AddComponent<VRCContactReceiver>();
            contact3.collisionTags = new List<string> { "TagA" };
    
            // Execute rename pass
            new RenameCollisionTagsPass().TestExecute(context);
    
            // Assert that Contact1 and Contact2 use Parent1's GUID
            Assert.That(contact1.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact2.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact1.collisionTags[0], Is.EqualTo(contact2.collisionTags[0]));
    
            // Assert that Contact3 uses Parent2's GUID
            Assert.That(contact3.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contact1.collisionTags[0], Is.Not.EqualTo(contact3.collisionTags[0]));
        }
    
        [Test]
        public void RenameTagsInDeepHierarchyConsistently()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
            var child = new GameObject("child");
            child.transform.parent = avatar.transform;
            var grandchild = new GameObject("grandchild");
            grandchild.transform.parent = child.transform;
    
            var renameCollisionTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameCollisionTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contactRoot = avatar.AddComponent<VRCContactReceiver>();
            contactRoot.collisionTags = new List<string> { "TagA" };
            var contactChild = child.AddComponent<VRCContactReceiver>();
            contactChild.collisionTags = new List<string> { "TagA" };
            var contactGrandchild = grandchild.AddComponent<VRCContactReceiver>();
            contactGrandchild.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contactRoot.collisionTags[0], Does.StartWith("TagA$"));
            Assert.That(contactRoot.collisionTags[0], Is.EqualTo(contactChild.collisionTags[0]));
            Assert.That(contactRoot.collisionTags[0], Is.EqualTo(contactGrandchild.collisionTags[0]));
        }
    
        [Test]
        public void EnsureNoChangeToEmptyCollisionTags()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string>();
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags, Is.Empty);
        }
    
        [Test]
        public void RenameTagsWithAutoRename()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Does.StartWith("TagA$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["TagA$".Length..], out _));
        }
    
        [Test]
        public void RenameTagsWithRenameTo()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = false, renameTo = "CustomTagA" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Is.EqualTo("CustomTagA"));
        }
    
        [Test]
        public void DoNotRenameWhenAutoRenameIsUncheckedAndRenameToIsEmpty()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = false, renameTo = "" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Is.EqualTo("TagA"));
        }
    
        [Test]
        public void RenameMultipleTagsWithMixedSettings()
        {
            var avatar = CreateRoot("avatar");
            var context = new BuildContext(avatar, null);
    
            var renameTags = avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            renameTags.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "TagA", autoRename = true, renameTo = "" },
                new() { name = "TagB", autoRename = false, renameTo = "CustomTagB" }
            };
    
            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "TagA", "TagB", "TagC" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            Assert.That(contact.collisionTags[0], Does.StartWith("TagA$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["TagA$".Length..], out _));
            Assert.That(contact.collisionTags[1], Is.EqualTo("CustomTagB"));
            Assert.That(contact.collisionTags[2], Is.EqualTo("TagC"));
        }
    
        [Test]
        public void NestedComponents_RenameChain_AppliesInOrder()
        {
            // Root
            var root = CreateRoot("Root");
            var context = new BuildContext(root, null);
    
            // A (Y -> Z)
            var goA = new GameObject("A");
            goA.transform.parent = root.transform;
            var compA = goA.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compA.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Y", autoRename = false, renameTo = "Z" }
            };
    
            // B (X -> Y)
            var goB = new GameObject("B");
            goB.transform.parent = goA.transform;
            var compB = goB.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compB.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "X", autoRename = false, renameTo = "Y" }
            };
    
            // C (Contact)
            var goC = new GameObject("C");
            goC.transform.parent = goB.transform;
            var contact = goC.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "X" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            // X -> Y (by B), then Y -> Z (by A), so result should be Z
            Assert.That(contact.collisionTags.Count, Is.EqualTo(1));
            Assert.That(contact.collisionTags[0], Is.EqualTo("Z"));
        }
    
        [Test]
        public void NestedComponents_AutoRenameNotOverriddenByParent()
        {
            // Root
            var root = CreateRoot("Root");
            var context = new BuildContext(root, null);
    
            // A (Y -> Z)
            var goA = new GameObject("A");
            goA.transform.parent = root.transform;
            var compA = goA.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compA.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Y", autoRename = false, renameTo = "Z" }
            };
    
            // B (autoRename X)
            var goB = new GameObject("B");
            goB.transform.parent = goA.transform;
            var compB = goB.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compB.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "X", autoRename = true, renameTo = "" }
            };
    
            // C (Contact)
            var goC = new GameObject("C");
            goC.transform.parent = goB.transform;
            var contact = goC.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "X" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            // X -> X$GUID (by B), should NOT be renamed to Z by A
            Assert.That(contact.collisionTags.Count, Is.EqualTo(1));
            Assert.That(contact.collisionTags[0], Does.StartWith("X$"));
            Assert.IsTrue(GUID.TryParse(contact.collisionTags[0]["X$".Length..], out _));
        }
    
        [Test]
        public void NestedComponents_MultipleRenamesInOneComponent()
        {
            // Root
            var root = CreateRoot("Root");
            var context = new BuildContext(root, null);
    
            // A (X -> Y, Y -> Z)
            var goA = new GameObject("A");
            goA.transform.parent = root.transform;
            var compA = goA.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compA.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "X", autoRename = false, renameTo = "Y" },
                new() { name = "Y", autoRename = false, renameTo = "Z" }
            };
    
            // B (Contact)
            var goB = new GameObject("B");
            goB.transform.parent = goA.transform;
            var contact = goB.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "X" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            // X -> Y, then Y -> Z, so result should be Z
            Assert.That(contact.collisionTags.Count, Is.EqualTo(1));
            Assert.That(contact.collisionTags[0], Is.EqualTo("Z"));
        }
    
        [Test]
        public void NestedComponents_SomeTagsNotRenamed_PartialMatch()
        {
            // Root
            var root = CreateRoot("Root");
            var context = new BuildContext(root, null);
    
            // A (Y -> Z)
            var goA = new GameObject("A");
            goA.transform.parent = root.transform;
            var compA = goA.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compA.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Y", autoRename = false, renameTo = "Z" }
            };
    
            // B (X -> Y)
            var goB = new GameObject("B");
            goB.transform.parent = goA.transform;
            var compB = goB.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compB.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "X", autoRename = false, renameTo = "Y" }
            };
    
            // C (Contact) with tags X, W, Y
            var goC = new GameObject("C");
            goC.transform.parent = goB.transform;
            var contact = goC.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "X", "W", "Y" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            // X -> Y (by B), then Y -> Z (by A), so X becomes Z
            // W is not renamed by any component, so remains W
            // Y is renamed by A to Z
            Assert.That(contact.collisionTags.Count, Is.EqualTo(2));
            Assert.That(contact.collisionTags, Does.Contain("Z"));
            Assert.That(contact.collisionTags, Does.Contain("W"));
            // Only one Z should be present (X and Y both become Z, but .Distinct() is used)
            Assert.That(contact.collisionTags.Count(t => t == "Z"), Is.EqualTo(1));
        }
    
        [Test]
        public void NestedComponents_MultipleTags_MixedRenames()
        {
            // Root
            var root = CreateRoot("Root");
            var context = new BuildContext(root, null);
    
            // A (Y -> Z, Q -> R)
            var goA = new GameObject("A");
            goA.transform.parent = root.transform;
            var compA = goA.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compA.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Y", autoRename = false, renameTo = "Z" },
                new() { name = "Q", autoRename = false, renameTo = "R" }
            };
    
            // B (X -> Y)
            var goB = new GameObject("B");
            goB.transform.parent = goA.transform;
            var compB = goB.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compB.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "X", autoRename = false, renameTo = "Y" }
            };
    
            // C (Contact) with tags X, Q, W
            var goC = new GameObject("C");
            goC.transform.parent = goB.transform;
            var contact = goC.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "X", "Q", "W" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            // X -> Y (by B), then Y -> Z (by A), so X becomes Z
            // Q -> R (by A)
            // W is not renamed
            Assert.That(contact.collisionTags.Count, Is.EqualTo(3));
            Assert.That(contact.collisionTags, Does.Contain("Z"));
            Assert.That(contact.collisionTags, Does.Contain("R"));
            Assert.That(contact.collisionTags, Does.Contain("W"));
        }
    
        [Test]
        public void NestedComponents_MultipleTags_SomeUnmatched()
        {
            // Root
            var root = CreateRoot("Root");
            var context = new BuildContext(root, null);
    
            // A (Y -> Z)
            var goA = new GameObject("A");
            goA.transform.parent = root.transform;
            var compA = goA.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compA.configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Y", autoRename = false, renameTo = "Z" }
            };
    
            // B (no configs)
            var goB = new GameObject("B");
            goB.transform.parent = goA.transform;
            var compB = goB.AddComponent<ModularAvatarRenameVRChatCollisionTags>();
            compB.configs = new List<RenameCollisionTagConfig>();
    
            // C (Contact) with tags Y, X
            var goC = new GameObject("C");
            goC.transform.parent = goB.transform;
            var contact = goC.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { "Y", "X" };
    
            new RenameCollisionTagsPass().TestExecute(context);
    
            // Y -> Z (by A), X is not renamed
            Assert.That(contact.collisionTags.Count, Is.EqualTo(2));
            Assert.That(contact.collisionTags, Does.Contain("Z"));
            Assert.That(contact.collisionTags, Does.Contain("X"));
        }

        [Test]
        public void AnimatorParametersUseContactCollisionTagMappings()
        {
            const string automaticTag = "Automatic_Tag";
            const string manualTag = "Manual_Tag";
            const string automaticParameter = "pb_CollisionTag_Automatic_Tag";
            const string manualParameter = "pb_CollisionTag_Manual_Tag";
            const string explicitParameter = "Explicit_Target";
            var avatar = CreateRoot("avatar");
            avatar.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = "pb",
                    remapTo = "pb2",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                },
                new()
                {
                    nameOrPrefix = manualParameter,
                    remapTo = explicitParameter,
                    syncType = ParameterSyncType.Bool
                }
            };
            avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>().configs = new List<RenameCollisionTagConfig>
            {
                new() { name = automaticTag, autoRename = true },
                new() { name = manualTag, autoRename = false, renameTo = "Manual_Renamed" },
                new() { name = "Duplicate_A", autoRename = false, renameTo = "Shared" },
                new() { name = "Duplicate_B", autoRename = false, renameTo = "Shared" }
            };

            var contact = avatar.AddComponent<VRCContactReceiver>();
            contact.collisionTags = new List<string> { automaticTag, manualTag };

            var clip = TrackObject(new AnimationClip());
            clip.SetCurve("", typeof(Animator), manualParameter, AnimationCurve.Constant(0, 1, 0));
            var blendTree = TrackObject(new BlendTree
            {
                blendType = BlendTreeType.Direct,
                blendParameter = automaticParameter,
                children = new[]
                {
                    new ChildMotion { motion = clip, directBlendParameter = manualParameter, timeScale = 1 }
                }
            });
            var controller = TrackObject(new AnimatorController
            {
                parameters = new[]
                {
                    new AnimatorControllerParameter { name = automaticParameter, type = AnimatorControllerParameterType.Bool },
                    new AnimatorControllerParameter { name = manualParameter, type = AnimatorControllerParameterType.Bool },
                    new AnimatorControllerParameter { name = "pb_CollisionTag_Duplicate_A", type = AnimatorControllerParameterType.Bool },
                    new AnimatorControllerParameter { name = "pb_CollisionTag_Duplicate_B", type = AnimatorControllerParameterType.Bool },
                    new AnimatorControllerParameter { name = "unrelated", type = AnimatorControllerParameterType.Bool }
                }
            });
            var stateMachine = TrackObject(new AnimatorStateMachine());
            var initialState = TrackObject(stateMachine.AddState("Initial"));
            var destinationState = TrackObject(stateMachine.AddState("Destination"));
            stateMachine.defaultState = initialState;
            initialState.motion = blendTree;
            initialState.AddTransition(destinationState).AddCondition(AnimatorConditionMode.If, 0, manualParameter);
            var duplicateTransition = initialState.AddTransition(destinationState);
            duplicateTransition.AddCondition(AnimatorConditionMode.If, 0, "pb_CollisionTag_Duplicate_A");
            duplicateTransition.AddCondition(AnimatorConditionMode.If, 0, "pb_CollisionTag_Duplicate_B");
            var driver = initialState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRCAvatarParameterDriver.Parameter>
            {
                new() { name = manualParameter }
            };
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Collision tags",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };
            avatar.AddComponent<ModularAvatarMergeAnimator>().animator = controller;

            AvatarProcessor.ProcessAvatar(avatar);

            var automaticRenamedTag = contact.collisionTags.Single(tag => tag.StartsWith(automaticTag + "$", StringComparison.Ordinal));
            Assert.That(contact.collisionTags, Does.Contain("Manual_Renamed"));
            Assert.IsTrue(Guid.TryParse(automaticRenamedTag[(automaticTag.Length + 1)..], out _));
            var automaticRemappedParameter = "pb2_CollisionTag_" + automaticRenamedTag;
            var duplicateRemappedParameter = "pb2_CollisionTag_Shared";
            var outputController = FindFxController(avatar).animatorController as AnimatorController;
            Assert.NotNull(outputController);
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Contain(automaticRemappedParameter));
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Contain(explicitParameter));
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Contain(duplicateRemappedParameter));
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Contain("unrelated"));
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Not.Contain(automaticParameter));
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Not.Contain(manualParameter));

            var outputState = outputController.layers.Single(layer => layer.name == "Collision tags").stateMachine.defaultState;
            Assert.That(outputState.transitions[0].conditions.Single().parameter, Is.EqualTo(explicitParameter));
            Assert.That(outputState.transitions[1].conditions.Select(condition => condition.parameter),
                Is.EqualTo(new[] { duplicateRemappedParameter, duplicateRemappedParameter }));
            var drivenParameters = outputState.behaviours.OfType<VRCAvatarParameterDriver>()
                .SelectMany(parameterDriver => parameterDriver.parameters)
                .Select(parameter => parameter.name)
                .ToList();
            Assert.That(drivenParameters, Does.Contain(explicitParameter));
            Assert.That(drivenParameters, Does.Not.Contain(manualParameter));
            var outputBlendTree = outputState.motion as BlendTree;
            Assert.That(outputBlendTree.blendParameter, Is.EqualTo(automaticRemappedParameter));
            Assert.That(outputBlendTree.children.Single().directBlendParameter, Is.EqualTo(explicitParameter));
            Assert.That(
                AnimationUtility.GetCurveBindings(outputBlendTree.children.Single().motion as AnimationClip)
                    .Select(binding => binding.propertyName),
                Is.EqualTo(new[] { explicitParameter })
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnimatorParametersUseNestedCollisionTagMappingsWithoutContacts(bool renamePrefix)
        {
            var avatar = CreateRoot("avatar");
            if (renamePrefix)
            {
                avatar.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
                {
                    new()
                    {
                        nameOrPrefix = "pb",
                        remapTo = "pb2",
                        isPrefix = true,
                        syncType = ParameterSyncType.Float
                    }
                };
            }
            avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>().configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Parent", autoRename = false, renameTo = "Child" }
            };
            var child = CreateChild(avatar, "child");
            child.AddComponent<ModularAvatarRenameVRChatCollisionTags>().configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Tag", autoRename = false, renameTo = "Parent" }
            };

            var parameter = "pb_CollisionTag_Tag";
            var controller = TrackObject(new AnimatorController
            {
                parameters = new[]
                {
                    new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Bool }
                }
            });
            var stateMachine = TrackObject(new AnimatorStateMachine());
            var initialState = TrackObject(stateMachine.AddState("Initial"));
            var destinationState = TrackObject(stateMachine.AddState("Destination"));
            stateMachine.defaultState = initialState;
            initialState.AddTransition(destinationState).AddCondition(AnimatorConditionMode.If, 0, parameter);
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Nested collision tags",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };
            child.AddComponent<ModularAvatarMergeAnimator>().animator = controller;

            AvatarProcessor.ProcessAvatar(avatar);

            var expected = (renamePrefix ? "pb2" : "pb") + "_CollisionTag_Child";
            var outputController = FindFxController(avatar).animatorController as AnimatorController;
            Assert.NotNull(outputController);
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Contain(expected));
            Assert.That(outputController.parameters.Select(parameter => parameter.name), Does.Not.Contain(parameter));
            var outputState = outputController.layers.Single(layer => layer.name == "Nested collision tags").stateMachine.defaultState;
            Assert.That(outputState.transitions.Single().conditions.Single().parameter, Is.EqualTo(expected));
        }

        [Test]
        public void RejectsAmbiguousCollisionTagAnimatorParameters()
        {
            const string twoSeparators = "pb_CollisionTag_Tag_CollisionTag_Extra";
            const string threeSeparators = "pb_CollisionTag_Tag_CollisionTag_Extra_CollisionTag_More";
            const string overlappingSeparators = "pb_CollisionTag_CollisionTag_Tag";
            const string outputAmbiguous = "pb2_CollisionTag_A_CollisionTag_B";
            var avatar = CreateRoot("avatar");
            avatar.AddComponent<ModularAvatarParameters>().parameters = new List<ParameterConfig>
            {
                new()
                {
                    nameOrPrefix = "pb",
                    remapTo = "pb2",
                    isPrefix = true,
                    syncType = ParameterSyncType.Float
                },
                new()
                {
                    nameOrPrefix = twoSeparators,
                    remapTo = "explicit",
                    syncType = ParameterSyncType.Bool
                }
            };
            avatar.AddComponent<ModularAvatarRenameVRChatCollisionTags>().configs = new List<RenameCollisionTagConfig>
            {
                new() { name = "Tag", autoRename = false, renameTo = "A_CollisionTag_B" }
            };
            var controller = TrackObject(new AnimatorController
            {
                parameters = new[]
                {
                    new AnimatorControllerParameter { name = twoSeparators, type = AnimatorControllerParameterType.Bool },
                    new AnimatorControllerParameter { name = overlappingSeparators, type = AnimatorControllerParameterType.Bool },
                    new AnimatorControllerParameter { name = "pb_CollisionTag_Tag", type = AnimatorControllerParameterType.Bool }
                }
            });
            var stateMachine = TrackObject(new AnimatorStateMachine());
            var initialState = TrackObject(stateMachine.AddState("Initial"));
            var destinationState = TrackObject(stateMachine.AddState("Destination"));
            stateMachine.defaultState = initialState;
            initialState.AddTransition(destinationState).AddCondition(AnimatorConditionMode.If, 0, threeSeparators);
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Ambiguous collision tags",
                    defaultWeight = 1,
                    stateMachine = stateMachine
                }
            };
            avatar.AddComponent<ModularAvatarMergeAnimator>().animator = controller;

            var errors = ErrorReport.CaptureErrors(() => AvatarProcessor.ProcessAvatar(avatar));
            var ambiguousParameters = errors.Select(error => error.TheError)
                .OfType<SimpleError>()
                .Where(error => error.TitleKey == "error.rename_collision_tags.ambiguous_parameter")
                .Select(error => error.DetailsSubst[0])
                .ToList();

            Assert.That(ambiguousParameters, Does.Contain(twoSeparators));
            Assert.That(ambiguousParameters, Does.Contain(threeSeparators));
            Assert.That(ambiguousParameters, Does.Contain(overlappingSeparators));
            Assert.That(ambiguousParameters, Does.Contain(outputAmbiguous));
        }
    }
}

#endif
