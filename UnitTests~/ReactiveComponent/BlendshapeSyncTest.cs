#if MA_VRCSDK3_AVATARS

using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
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

            var m1 = analysis.Shapes[new TargetProp()
            {
                TargetObject = root.transform.Find("m1").GetComponent<SkinnedMeshRenderer>(),
                PropertyName = "blendShape.bottom"
            }];
            var m2 = analysis.Shapes[new TargetProp()
            {
                TargetObject = root.transform.Find("m2").GetComponent<SkinnedMeshRenderer>(),
                PropertyName = "blendShape.bottom"
            }];
            var m3 = analysis.Shapes[new TargetProp()
            {
                TargetObject = root.transform.Find("m3").GetComponent<SkinnedMeshRenderer>(),
                PropertyName = "blendShape.top"
            }];
            
            Assert.IsTrue(analysis.Shapes.ContainsKey(new TargetProp()
            {
                TargetObject = root.transform.Find("m1").GetComponent<SkinnedMeshRenderer>(),
                PropertyName = "deletedShape.bottom"
            }));
            
            Assert.AreEqual(4, analysis.Shapes.Count);

            foreach (var ag in m1.actionGroups)
            {
                ag.TargetProp = new TargetProp();
            }
            
            foreach (var ag in m2.actionGroups)
            {
                ag.TargetProp = new TargetProp();
            }
            
            foreach (var ag in m3.actionGroups)
            {
                ag.TargetProp = new TargetProp();
            }
            
            Assert.AreEqual(m2.actionGroups, m1.actionGroups);
            Assert.AreEqual(m3.actionGroups, m1.actionGroups);
        }
    }
}

#endif