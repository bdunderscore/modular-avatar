using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests.MergeArmatureTests
{
    public class TestComponentA : MonoBehaviour
    {
    }

    public class TestComponentB : MonoBehaviour
    {
    }

    public class MarkDestroy : MonoBehaviour
    {
        private void OnDestroy()
        {
            Debug.Log("blah");
        }
    }

    public class MultiLevelMergeTest : TestBase
    {
        [Test]
        public void mergeProcessesInTopoOrder()
        {
            var root = CreateRoot("root");
            var armature = CreateChild(root, "Armature");
            var bone = CreateChild(armature, "Bone");

            var merge1 = CreateChild(root, "merge1");
            var m1_bone = CreateChild(merge1, "Bone");
            var m1_leaf = CreateChild(m1_bone, "leaf1");
            var m1_leaf2 = CreateChild(m1_leaf, "leaf2");

            var merge2 = CreateChild(root, "merge2");
            var m2_bone = CreateChild(merge2, "Bone");
            var m2_leaf = CreateChild(m2_bone, "leaf1");
            var m2_leaf3 = CreateChild(m2_leaf, "leaf3");

            var ma1 = merge1.AddComponent<ModularAvatarMergeArmature>();
            ma1.mergeTarget.referencePath = RuntimeUtil.AvatarRootPath(armature);

            var ma2 = merge2.AddComponent<ModularAvatarMergeArmature>();
            ma2.mergeTarget.referencePath = RuntimeUtil.AvatarRootPath(merge1);

            m1_leaf2.AddComponent<TestComponentA>();
            m2_leaf3.AddComponent<TestComponentB>();

            nadena.dev.ndmf.BuildContext context =
                new nadena.dev.ndmf.BuildContext(root.GetComponent<VRCAvatarDescriptor>(), null);
            context.ActivateExtensionContext<ModularAvatarContext>();
            context.ActivateExtensionContext<AnimationServicesContext>();
            new MergeArmatureHook().OnPreprocessAvatar(context, root);

            Assert.IsTrue(bone.GetComponentInChildren<TestComponentA>() != null);
            Assert.IsTrue(bone.GetComponentInChildren<TestComponentB>() != null);
            Assert.IsTrue(m2_leaf3.GetComponentsInParent<Transform>().Contains(m1_leaf.transform));
        }

        [Test]
        public void canDisableNameMangling()
        {
            var root = CreateRoot("root");
            var armature = CreateChild(root, "Armature");
            var bone = CreateChild(armature, "Bone");

            var merge = CreateChild(root, "merge");
            var m_bone = CreateChild(merge, "Bone");
            var m_leaf = CreateChild(m_bone, "leaf");

            //m_bone.AddComponent<MarkDestroy>();

            var ma = merge.AddComponent<ModularAvatarMergeArmature>();
            ma.mergeTarget.referencePath = RuntimeUtil.AvatarRootPath(armature);
            ma.mangleNames = false;

            nadena.dev.ndmf.BuildContext context =
                new nadena.dev.ndmf.BuildContext(root.GetComponent<VRCAvatarDescriptor>(), null);
            context.ActivateExtensionContext<ModularAvatarContext>();
            context.ActivateExtensionContext<AnimationServicesContext>();
            new MergeArmatureHook().OnPreprocessAvatar(context, root);

            Assert.IsTrue(m_bone == null); // destroyed by retargeting pass
            Assert.IsTrue(m_leaf.transform.name == "leaf");
        }

        [Test]
        public void manglesByDefault()
        {
            var root = CreateRoot("root");
            var armature = CreateChild(root, "Armature");
            var bone = CreateChild(armature, "Bone");

            var merge = CreateChild(root, "merge");
            var m_bone = CreateChild(merge, "Bone");
            var m_leaf = CreateChild(m_bone, "leaf");

            var ma = merge.AddComponent<ModularAvatarMergeArmature>();
            ma.mergeTarget.referencePath = RuntimeUtil.AvatarRootPath(armature);

            nadena.dev.ndmf.BuildContext context =
                new nadena.dev.ndmf.BuildContext(root.GetComponent<VRCAvatarDescriptor>(), null);
            context.ActivateExtensionContext<ModularAvatarContext>();
            context.ActivateExtensionContext<AnimationServicesContext>();
            new MergeArmatureHook().OnPreprocessAvatar(context, root);

            Assert.IsTrue(m_bone == null); // destroyed by retargeting pass
            Assert.IsTrue(m_leaf.transform.name != "leaf");
        }
    }
}