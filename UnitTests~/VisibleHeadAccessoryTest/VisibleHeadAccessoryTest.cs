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
using VRC.SDK3.Avatars.Components;

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
                "Armature/Hips/Spine/Chest/Neck/Head/Head (HeadChop)/O1",
                "Armature/Hips/Spine/Chest/Neck/Head/Head (HeadChop)/O1/O2",
                "Armature/Hips/Spine/Chest/Neck/Head/Head (HeadChop)/O1/O2/Cube",
            }.ToImmutableSortedSet();

            var bindings = AnimationUtility.GetCurveBindings(fx_anim).Select(binding => binding.path)
                .ToImmutableSortedSet();
            Assert.AreEqual(expectedBindings, bindings);

            var head = prefab.transform.Find("Armature/Hips/Spine/Chest/Neck/Head");
            var chop = head.Find("Head (HeadChop)");
            var headchop = chop.GetComponent<VRCHeadChop>();

            Assert.AreEqual(headchop.targetBones.Length, 1);
            Assert.AreEqual(headchop.targetBones[0].transform, chop);
            Assert.AreEqual(headchop.targetBones[0].scaleFactor, 1);
            Assert.AreEqual(headchop.targetBones[0].applyCondition, VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply);
            Assert.AreEqual(headchop.globalScaleFactor, 1);
        }
    }
}