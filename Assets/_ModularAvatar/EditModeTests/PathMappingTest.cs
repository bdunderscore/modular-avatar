using System.Collections;
using System.Collections.Generic;
using System.IO;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;

namespace modular_avatar_tests
{
    public class PathMappingTest : TestBase
    {
        [Test]
        public void TracksSimpleRenames()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");

            PathMappings.Init(root);
            Assert.AreEqual("a", PathMappings.MapPath("a"));
            a.name = "b";
            PathMappings.ClearCache();
            Assert.AreEqual("b", PathMappings.MapPath("a"));
        }

        [Test]
        public void TracksObjectMoves()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(root, "b");

            PathMappings.Init(root);
            Assert.AreEqual("a", PathMappings.MapPath("a"));
            a.transform.parent = b.transform;
            PathMappings.ClearCache();
            Assert.AreEqual("b/a", PathMappings.MapPath("a"));
        }

        [Test]
        public void TracksCollapses()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");

            PathMappings.Init(root);
            PathMappings.MarkRemoved(b);
            c.transform.parent = a.transform;
            Object.DestroyImmediate(b);

            Assert.AreEqual("a/c", PathMappings.MapPath("a/b/c"));
        }

        [Test]
        public void TransformLookthrough()
        {
            var root = CreateRoot("root");
            var a = CreateChild(root, "a");
            var b = CreateChild(a, "b");
            var c = CreateChild(b, "c");
            var d = CreateChild(c, "d");

            PathMappings.Init(root);
            PathMappings.MarkTransformLookthrough(b);
            PathMappings.MarkTransformLookthrough(c);
            Assert.AreEqual("a/b/c", PathMappings.MapPath("a/b/c"));
            Assert.AreEqual("a", PathMappings.MapPath("a/b/c", true));
            Assert.AreEqual("a/b/c/d", PathMappings.MapPath("a/b/c/d", true));
        }
    }
}