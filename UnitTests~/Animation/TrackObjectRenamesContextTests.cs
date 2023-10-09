using System.Linq;
using nadena.dev.modular_avatar.animation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace modular_avatar_tests
{
    using UnityObject = UnityEngine.Object;

    public class TrackObjectRenamesContextTests : TestBase
    {
        [Test]
        public void testBasicContextInitialization()
        {
            var av = CreateRoot("root");

            var bc = CreateContext(av);
            var toc = new AnimationServicesContext();

            toc.OnActivate(bc);
            toc.OnDeactivate(bc);
        }


        [Test]
        public void TracksSimpleRenames()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");

            var toc = new AnimationServicesContext();
            toc.OnActivate(CreateContext(root));
            Assert.AreEqual("a", toc.PathMappings.MapPath("a"));
            a.name = "b";
            toc.PathMappings.ClearCache();
            Assert.AreEqual("b", toc.PathMappings.MapPath("a"));
        }

        [Test]
        public void TracksObjectMoves()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(root, "b");

            var toc = new AnimationServicesContext();
            toc.OnActivate(CreateContext(root));

            Assert.AreEqual("a", toc.PathMappings.MapPath("a"));
            a.transform.parent = b.transform;
            toc.PathMappings.ClearCache();
            Assert.AreEqual("b/a", toc.PathMappings.MapPath("a"));
        }

        [Test]
        public void TracksCollapses()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");

            var toc = new AnimationServicesContext();
            toc.OnActivate(CreateContext(root));

            toc.PathMappings.MarkRemoved(b);
            c.transform.parent = a.transform;
            UnityObject.DestroyImmediate(b);

            Assert.AreEqual("a/c", toc.PathMappings.MapPath("a/b/c"));
        }

        [Test]
        public void TransformLookthrough()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");
            var d = CreateChild(c, "d");

            var toc = new AnimationServicesContext();
            toc.OnActivate(CreateContext(root));

            toc.PathMappings.MarkTransformLookthrough(b);
            toc.PathMappings.MarkTransformLookthrough(c);
            Assert.AreEqual("a/b/c", toc.PathMappings.MapPath("a/b/c"));
            Assert.AreEqual("a", toc.PathMappings.MapPath("a/b/c", true));
            Assert.AreEqual("a/b/c/d", toc.PathMappings.MapPath("a/b/c/d", true));
        }

#if MA_VRCSDK3_AVATARS
        [Test]
        public void TestAnimatorControllerUpdates()
        {
            var root = CreatePrefab("BasicObjectReferenceTest.prefab");
            var parent = root.transform.Find("parent").gameObject;
            var child = parent.transform.Find("child").gameObject;

            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            var oldFx = descriptor.baseAnimationLayers.First(l =>
                l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var oldIk = descriptor.specialAnimationLayers.First(l =>
                l.type == VRCAvatarDescriptor.AnimLayerType.IKPose);

            var toc = new AnimationServicesContext();
            var buildContext = CreateContext(root);
            toc.OnActivate(buildContext);
            toc.PathMappings.MarkTransformLookthrough(child);

            parent.name = "p2";

            toc.OnDeactivate(buildContext);

            var newFx = buildContext.AvatarDescriptor.baseAnimationLayers.First(l =>
                l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var newIk = buildContext.AvatarDescriptor.specialAnimationLayers.First(l =>
                l.type == VRCAvatarDescriptor.AnimLayerType.IKPose);

            Assert.AreNotEqual(oldFx.animatorController, newFx.animatorController);
            Assert.AreNotEqual(oldIk.animatorController, newIk.animatorController);

            CheckClips(newFx.animatorController as AnimatorController);
            CheckClips(newIk.animatorController as AnimatorController);

            void CheckClips(AnimatorController controller)
            {
                var clip = controller.layers[0].stateMachine.states[0].state.motion
                    as AnimationClip;

                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type == typeof(Transform))
                    {
                        Assert.AreEqual("p2", binding.path);
                    }
                    else
                    {
                        Assert.AreEqual("p2/child", binding.path);
                    }
                }
            }
        }
#endif
    }
}