#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.ReactiveComponent
{
    public class BlendshapeSyncTest : TestBase
    {
        [Test]
        public void blendshapeSync_propagatesThroughMeshes()
        {
            var root = CreatePrefab("BlendshapeSyncTest.prefab");

            var analysis = new ReactiveObjectAnalyzer().Analyze(root);

            var m1 = analysis.Shapes[new ShapeKeyTarget(
                root.transform.Find("m1").GetComponent<SkinnedMeshRenderer>(), "bottom")];
            var m2 = analysis.Shapes[new ShapeKeyTarget(
                root.transform.Find("m2").GetComponent<SkinnedMeshRenderer>(), "bottom")];
            var m3 = analysis.Shapes[new ShapeKeyTarget(
                root.transform.Find("m3").GetComponent<SkinnedMeshRenderer>(), "top")];
            
            AssertActionGroupsEqualIgnoringRenderer(m1.actionGroups, m2.actionGroups);
            AssertActionGroupsEqualIgnoringRenderer(m1.actionGroups, m3.actionGroups);
        }

        private static void AssertActionGroupsEqualIgnoringRenderer(
            System.Collections.Generic.IReadOnlyList<ReactionRule> expected,
            System.Collections.Generic.IReadOnlyList<ReactionRule> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                var expectedRule = expected[i];
                var actualRule = actual[i];
                CollectionAssert.AreEqual(expectedRule.ControllingConditions, actualRule.ControllingConditions);
                Assert.AreEqual(expectedRule.Inverted, actualRule.Inverted);

                Assert.That(expectedRule.Action, Is.TypeOf<SetShapeKey>());
                Assert.That(actualRule.Action, Is.TypeOf<SetShapeKey>());
                var expectedAction = (SetShapeKey) expectedRule.Action;
                var actualAction = (SetShapeKey) actualRule.Action;
                Assert.AreEqual(expectedAction.Value, actualAction.Value);
            }
        }
    }
}

#endif