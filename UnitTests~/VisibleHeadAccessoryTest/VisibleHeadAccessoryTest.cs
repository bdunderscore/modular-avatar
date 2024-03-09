using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace UnitTests.VisibleHeadAccessoryTest
{
    internal class VisibleHeadAccessoryTest : TestBase
    {
        [Test]
        [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")] // false positive
        public void Test()
        {
            var prefab = CreatePrefab("VHA_shapell.prefab");
            
            var cubeObj = prefab.transform.Find(
                "Armature/Hips/Spine/Chest/Neck/Head/O1/O2/Cube"
            );
            var priorWorldPos = cubeObj.position;
            var priorWorldRot = cubeObj.rotation;
            
            AvatarProcessor.ProcessAvatar(prefab);
            
            Assert.Less((cubeObj.position - priorWorldPos).magnitude, 0.001f);
            Assert.Less(Quaternion.Angle(cubeObj.rotation, priorWorldRot), 0.001f);

            var fx_anim = findFxClip(prefab, "test");

            var expectedBindings = new HashSet<string>()
            {
                "Armature/Hips/Spine/Chest/Neck/Head/O1",
                "Armature/Hips/Spine/Chest/Neck/Head/O1/O2",
                "Armature/Hips/Spine/Chest/Neck/Head (FirstPersonVisible)/O1",
                "Armature/Hips/Spine/Chest/Neck/Head (FirstPersonVisible)/O1/O2",
                "Armature/Hips/Spine/Chest/Neck/Head (FirstPersonVisible)/O1/O2/Cube",
            }.ToImmutableSortedSet();

            var bindings = AnimationUtility.GetCurveBindings(fx_anim).Select(binding => binding.path)
                .ToImmutableSortedSet();
            Assert.AreEqual(expectedBindings, bindings);

            var head = prefab.transform.Find("Armature/Hips/Spine/Chest/Neck/Head");
            var constraint = prefab.transform.Find("Armature/Hips/Spine/Chest/Neck/Head (FirstPersonVisible)")
                .GetComponent<ParentConstraint>();

            Assert.AreEqual(head, constraint.GetSource(0).sourceTransform);
            Assert.AreEqual(1, constraint.GetSource(0).weight);
            Assert.AreEqual(new Vector3(0, 0, 0), constraint.translationOffsets[0]);
            Assert.AreEqual(new Vector3(0, 0, 0), constraint.rotationOffsets[0]);
        }
    }
}