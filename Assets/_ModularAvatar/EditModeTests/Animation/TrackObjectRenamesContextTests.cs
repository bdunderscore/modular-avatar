using System.Linq;
using nadena.dev.ndmf.animation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

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
            var toc = new TrackObjectRenamesContext();

            toc.OnActivate(bc);
            toc.OnDeactivate(bc);
        }


        [Test]
        public void TracksSimpleRenames()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));
            Assert.AreEqual("a", toc.MapPath("a"));
            a.name = "b";
            toc.ClearCache();
            Assert.AreEqual("b", toc.MapPath("a"));
        }

        [Test]
        public void TracksObjectMoves()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(root, "b");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));

            Assert.AreEqual("a", toc.MapPath("a"));
            a.transform.parent = b.transform;
            toc.ClearCache();
            Assert.AreEqual("b/a", toc.MapPath("a"));
        }

        [Test]
        public void TracksCollapses()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));

            toc.MarkRemoved(b);
            c.transform.parent = a.transform;
            UnityObject.DestroyImmediate(b);

            Assert.AreEqual("a/c", toc.MapPath("a/b/c"));
        }

        [Test]
        public void TransformLookthrough()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");
            var d = CreateChild(c, "d");

            var toc = new TrackObjectRenamesContext();
            toc.OnActivate(CreateContext(root));

            toc.MarkTransformLookthrough(b);
            toc.MarkTransformLookthrough(c);
            Assert.AreEqual("a/b/c", toc.MapPath("a/b/c"));
            Assert.AreEqual("a", toc.MapPath("a/b/c", true));
            Assert.AreEqual("a/b/c/d", toc.MapPath("a/b/c/d", true));
        }

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

            var toc = new TrackObjectRenamesContext();
            var buildContext = CreateContext(root);
            toc.OnActivate(buildContext);
            toc.MarkTransformLookthrough(child);

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
    }
}