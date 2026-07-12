#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using NUnit.Framework;
using UnitTests.SharedInterfaces;
using UnityEditor;
using UnityEngine;

namespace modular_avatar_tests
{
    using RemapCurve = ModularAvatarBlendshapeSync.RemapCurve;

    public class BlendShapeSyncRemapCurveTest: TestBase
    {
        #region RemapCurve Utility class

        public static IEnumerable<TestCaseData> IdentityCurve_Cases()
        {
            yield return new TestCaseData(null);
            yield return new TestCaseData(new AnimationCurve()).SetName("EmptyCurve");
            yield return new TestCaseData(new AnimationCurve(new Keyframe(-1, 0))).SetName("SingleKeyframe");
            yield return new TestCaseData(new AnimationCurve(NewLinearFrame(0, 0), NewLinearFrame(1, 1))).SetName("ValidShortIdentityCurve");
            yield return new TestCaseData(new AnimationCurve(NewLinearFrame(0, 0), NewLinearFrame(100, 100))).SetName("ValidIdentityCurve");
        }

        [Test]
        [TestCaseSource(nameof(IdentityCurve_Cases))]
        public void IdentityCurve(AnimationCurve? inCurve)
        {
            var remapCurve = new RemapCurve(null);
            Assert.That(remapCurve.IsIdentity, Is.True);
            Assert.That(remapCurve.SplitPoints, Is.Empty);

            Assert.That(remapCurve.GetPointOnCurve(-50).OriginalValue, Is.EqualTo(-50));
            Assert.That(remapCurve.GetPointOnCurve(-50).MappedValue, Is.EqualTo(-50));
            Assert.That(remapCurve.GetPointOnCurve(-50).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(-50).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(0).OriginalValue, Is.EqualTo(0));
            Assert.That(remapCurve.GetPointOnCurve(0).MappedValue, Is.EqualTo(0));
            Assert.That(remapCurve.GetPointOnCurve(0).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(0).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(50).OriginalValue, Is.EqualTo(50));
            Assert.That(remapCurve.GetPointOnCurve(50).MappedValue, Is.EqualTo(50));
            Assert.That(remapCurve.GetPointOnCurve(50).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(50).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(100).OriginalValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(100).MappedValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(100).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(100).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(120).OriginalValue, Is.EqualTo(120));
            Assert.That(remapCurve.GetPointOnCurve(120).MappedValue, Is.EqualTo(120));
            Assert.That(remapCurve.GetPointOnCurve(120).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(120).RightDerivative, Is.EqualTo(1));
        }

        [Test]
        public void IdentityLikeWithSingleSplitPoint()
        {
            var remapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(50, 50),
                NewLinearFrame(100, 100)
            ));
            Assert.That(remapCurve.IsIdentity, Is.False);

            // since the curve is identity-like, GetPointOnCurve behavior should be same as identity curve
            Assert.That(remapCurve.GetPointOnCurve(-50).MappedValue, Is.EqualTo(-50));
            Assert.That(remapCurve.GetPointOnCurve(-50).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(-50).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(0).OriginalValue, Is.EqualTo(0));
            Assert.That(remapCurve.GetPointOnCurve(0).MappedValue, Is.EqualTo(0));
            Assert.That(remapCurve.GetPointOnCurve(0).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(0).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(50).MappedValue, Is.EqualTo(50));
            Assert.That(remapCurve.GetPointOnCurve(50).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(50).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(100).MappedValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(100).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(100).RightDerivative, Is.EqualTo(1));

            Assert.That(remapCurve.GetPointOnCurve(120).MappedValue, Is.EqualTo(120));
            Assert.That(remapCurve.GetPointOnCurve(120).LeftDerivative, Is.EqualTo(1));
            Assert.That(remapCurve.GetPointOnCurve(120).RightDerivative, Is.EqualTo(1));

            // However, there is a middle key frame; we should split the source curve.
            // the point won't change derivative, though.
            Assert.That(remapCurve.SplitPoints.Count(), Is.EqualTo(1));
            var splitPoint = remapCurve.SplitPoints.First();
            Assert.That(splitPoint.OriginalValue, Is.EqualTo(50));
            Assert.That(splitPoint.MappedValue, Is.EqualTo(50));
            Assert.That(splitPoint.LeftDerivative, Is.EqualTo(1));
            Assert.That(splitPoint.RightDerivative, Is.EqualTo(1));
            Assert.That(splitPoint.MapTangent(1, isOut: false), Is.EqualTo(1));
        }

        [Test]
        public void MapTangent()
        {
            RemapCurve.Point pointOnCurve;

            var remapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame( 25, 0),
                NewLinearFrame( 50, 100),
                NewLinearFrame( 75, 0),
                NewLinearFrame(100, 0)
            ));
            var split25 = remapCurve.SplitPoints.First();
            var split50 = remapCurve.SplitPoints.Skip(1).First();
            var split75 = remapCurve.SplitPoints.Skip(2).First();

            // The flat 0 segment: become 0
            pointOnCurve = remapCurve.GetPointOnCurve(12.5f);
            Assert.That(pointOnCurve.LeftDerivative, Is.EqualTo(0));
            Assert.That(pointOnCurve.RightDerivative, Is.EqualTo(0));
            Assert.That(pointOnCurve.MapTangent(10, isOut: false), Is.EqualTo(0));
            Assert.That(pointOnCurve.MapTangent(10, isOut: true), Is.EqualTo(0));
            Assert.That(pointOnCurve.MapTangent(-10, isOut: false), Is.EqualTo(0));
            Assert.That(pointOnCurve.MapTangent(-10, isOut: true), Is.EqualTo(0));
            // infinity should be infinity as-is. multiplying derivatives leads to NaN but should not.
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: false), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: true), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: false), Is.EqualTo(float.NegativeInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: true), Is.EqualTo(float.NegativeInfinity));

            // The point changes flat 0 => positive
            pointOnCurve = remapCurve.GetPointOnCurve(25.0f);
            Assert.That(pointOnCurve, Is.EqualTo(split25));
            Assert.That(pointOnCurve.LeftDerivative, Is.EqualTo(0));
            Assert.That(pointOnCurve.RightDerivative, Is.EqualTo(4));
            // for increasing tangent, the outing side should be multiplied with RightDerivative
            Assert.That(pointOnCurve.MapTangent(10, isOut: false), Is.EqualTo(0));
            Assert.That(pointOnCurve.MapTangent(10, isOut: true), Is.EqualTo(40));
            // for deceasing tangent, the incoming side should be multiplied with RightDerivative
            Assert.That(pointOnCurve.MapTangent(-10, isOut: false), Is.EqualTo(-40));
            Assert.That(pointOnCurve.MapTangent(-10, isOut: true), Is.EqualTo(0));
            // infinity as-is. sign of infinity selects which value is used for the constant segment by representing earlier/later, not greater/lesser.
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: false), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: true), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: false), Is.EqualTo(float.NegativeInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: true), Is.EqualTo(float.NegativeInfinity));

            // The point changes positive => negative
            pointOnCurve = remapCurve.GetPointOnCurve(50.0f);
            Assert.That(pointOnCurve, Is.EqualTo(split50));
            Assert.That(pointOnCurve.LeftDerivative, Is.EqualTo(4));
            Assert.That(pointOnCurve.RightDerivative, Is.EqualTo(-4));
            // for increasing tangent, the outing side will change their sign
            Assert.That(pointOnCurve.MapTangent(10, isOut: false), Is.EqualTo(40));
            Assert.That(pointOnCurve.MapTangent(10, isOut: true), Is.EqualTo(-40));
            // for deceasing tangent, the incoming side will change their sign
            Assert.That(pointOnCurve.MapTangent(-10, isOut: false), Is.EqualTo(40));
            Assert.That(pointOnCurve.MapTangent(-10, isOut: true), Is.EqualTo(-40));
            // infinity as-is even when the derivative of the map curve is 
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: false), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: true), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: false), Is.EqualTo(float.NegativeInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: true), Is.EqualTo(float.NegativeInfinity));
            
            // The point changes negative => flat 0
            pointOnCurve = remapCurve.GetPointOnCurve(75.0f);
            Assert.That(pointOnCurve, Is.EqualTo(split75));
            Assert.That(pointOnCurve.LeftDerivative, Is.EqualTo(-4));
            Assert.That(pointOnCurve.RightDerivative, Is.EqualTo(0));
            // for increasing tangent, the incoming side will change their sign
            Assert.That(pointOnCurve.MapTangent(10, isOut: false), Is.EqualTo(-40));
            Assert.That(pointOnCurve.MapTangent(10, isOut: true), Is.EqualTo(0));
            // for deceasing tangent, the outgoing side will change their sign
            Assert.That(pointOnCurve.MapTangent(-10, isOut: false), Is.EqualTo(0));
            Assert.That(pointOnCurve.MapTangent(-10, isOut: true), Is.EqualTo(40));
            // infinity as-is even when the derivative of the map curve is 
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: false), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.PositiveInfinity, isOut: true), Is.EqualTo(float.PositiveInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: false), Is.EqualTo(float.NegativeInfinity));
            Assert.That(pointOnCurve.MapTangent(float.NegativeInfinity, isOut: true), Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void MappedValue()
        {
            var remapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(50, 100),
                NewLinearFrame(100, 100)
            ));

            // mapped value exactly at remap curve keyframe
            Assert.That(remapCurve.GetPointOnCurve(0).MappedValue, Is.EqualTo(0));
            Assert.That(remapCurve.GetPointOnCurve(50).MappedValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(100).MappedValue, Is.EqualTo(100));
            // between value
            Assert.That(remapCurve.GetPointOnCurve(25).MappedValue, Is.EqualTo(50));
            Assert.That(remapCurve.GetPointOnCurve(75).MappedValue, Is.EqualTo(100));
            // out of range: extend tangent at edge
            Assert.That(remapCurve.GetPointOnCurve(125).MappedValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(150).MappedValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(200).MappedValue, Is.EqualTo(100));
            Assert.That(remapCurve.GetPointOnCurve(-25).MappedValue, Is.EqualTo(-50));
            Assert.That(remapCurve.GetPointOnCurve(-50).MappedValue, Is.EqualTo(-100));
            Assert.That(remapCurve.GetPointOnCurve(-100).MappedValue, Is.EqualTo(-200));
        }

        #endregion

        #region MapCurve Known Cases

        [Test]
        public void SplitExactlyAtInfinityTangentPointWouldNotCreateInfinityTangent()
        {
            // For a Bezier curve segment with both weights = 1, the time-axis derivative becomes 0
            // exactly at the middle point. Therefore, their time derivative of the value will be infinity.
            // The unity animation curve editor will create a keyframe with infinity tangent and constant segment
            // for both sides when we add a keyframe at the middle point.
            // However, we must not break their behavior, so we will create two frames around the middle point
            // to eliminate infinity tangent.

            var sourceCurve = new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0, inWeight: 1 / 3f, outWeight: 1),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0, inWeight: 1, outWeight: 1 / 3f)
            );

            var mapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(50, 60),
                NewLinearFrame(100, 100)
            ));

            var mapped = BlendshapeSyncAnimationProcessor.MapCurve(sourceCurve, mapCurve);

            Assert.That(mapped.keys.Length, Is.EqualTo(4));
            Assert.That(mapped.keys.All(x => float.IsFinite(x.inTangent) && float.IsFinite(x.outTangent)));
        }

        [Test]
        public void SplitNormalCurve()
        {
            var sourceCurve = new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0)
            );

            var mapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(50, 60),
                NewLinearFrame(100, 100)
            ));

            var mapped = BlendshapeSyncAnimationProcessor.MapCurve(sourceCurve, mapCurve);

            Assert.That(mapped.keys.Length, Is.EqualTo(3));
            Assert.That(mapped.keys[1].time, Is.EqualTo(0.5f));
            Assert.That(mapped.keys[1].value, Is.EqualTo(60f).Within(0.0001f));
        }

        [Test]
        public void NoSplitForNonCrossingSegment()
        {
            // If the source curve does not cross any split point, the segment will not be cut.

            var sourceCurve = new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 30, inTangent: 0, outTangent: 0)
            );

            var mapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(50, 60),
                NewLinearFrame(100, 100)
            ));

            var mapped = BlendshapeSyncAnimationProcessor.MapCurve(sourceCurve, mapCurve);

            Assert.That(mapped.keys.Length, Is.EqualTo(2));
        }

        public static IEnumerable<TestCaseData> NoSplitForConstantSegment_Cases()
        {
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: float.PositiveInfinity),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0)
            )).SetName("tangent=(PositiveInfinity, 0)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: float.NegativeInfinity),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0)
            )).SetName("tangent=(NegativeInfinity, 0)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.PositiveInfinity, outTangent: 0)
            )).SetName("tangent=(0,PositiveInfinity)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.NegativeInfinity, outTangent: 0)
            )).SetName("tangent=(0,NegativeInfinity)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.PositiveInfinity, outTangent: float.PositiveInfinity)
            )).SetName("tangent=(PositiveInfinity,PositiveInfinity)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.PositiveInfinity, outTangent: float.NegativeInfinity)
            )).SetName("tangent=(PositiveInfinity,NegativeInfinity)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.NegativeInfinity, outTangent: float.PositiveInfinity)
            )).SetName("tangent=(NegativeInfinity,PositiveInfinity)");
            yield return new TestCaseData(new AnimationCurve(
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.NegativeInfinity, outTangent: float.NegativeInfinity)
            )).SetName("tangent=(NegativeInfinity,NegativeInfinity)");
        }

        [Test]
        [TestCaseSource(nameof(NoSplitForConstantSegment_Cases))]
        public void NoSplitForConstantSegment(AnimationCurve sourceCurve)
        {
            // Even when key frame value crosses a split point, if the segment is constant segment with Infinity tangent, the segment will not be cut.

            var mapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(50, 60),
                NewLinearFrame(100, 100)
            ));

            var mapped = BlendshapeSyncAnimationProcessor.MapCurve(sourceCurve, mapCurve);

            Assert.That(mapped.keys.Length, Is.EqualTo(2));
        }

        [Test]
        public void ReturnsExactlySameCurveForIdentityRemapCurve()
        {
            // If the source curve does not cross with split point, the segment will not be cut.

            var sourceCurve = new AnimationCurve(
                new Keyframe(
                    time: 0,
                    value: 0,
                    inTangent: 0,
                    outTangent: 0
                ),
                new Keyframe(
                    time: 1,
                    value: 30,
                    inTangent: 0,
                    outTangent: 0
                )
            );

            var mapCurve = new RemapCurve(new AnimationCurve(
                NewLinearFrame(0, 0),
                NewLinearFrame(100, 100)
            ));
            Assert.That(mapCurve.IsIdentity, Is.True);

            var mapped = BlendshapeSyncAnimationProcessor.MapCurve(sourceCurve, mapCurve);

            Assert.That(mapped, Is.SameAs(sourceCurve));
        }

        #endregion

        #region MapCurve

        private static readonly int Seed = UnityEngine.Random.Range(0, int.MaxValue);

        public static IEnumerable<AnimationCurveCase> TestCurves()
        {
            var curveSource = ITestSupport.Instance.LoadAsset<AnimationClip>(typeof(BlendShapeSyncRemapCurveTest), "BlendshapeSyncRemapCurveTestCurveClip.anim");
            var curve = AnimationUtility.GetEditorCurve(curveSource, AnimationUtility.GetCurveBindings(curveSource)[0]);

            yield return new AnimationCurveCase("Saved Curve", BlendshapeSyncAnimationProcessor.NormalizeCurveToFreeTangents(curve));
            yield return new AnimationCurveCase("Bezier with Infinity Tangent",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0, inWeight: 1 / 3f, outWeight: 1),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0, inWeight: 1, outWeight: 1 / 3f)
            );
            // with long segment, the segment split might become visible
            yield return new AnimationCurveCase("Long Bezier with Infinity Tangent",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0, inWeight: 1 / 3f, outWeight: 1),
                new Keyframe(time: 200, value: 100, inTangent: 0, outTangent: 0, inWeight: 1, outWeight: 1 / 3f)
            );
            yield return new AnimationCurveCase("Long Bezier with Infinity Tangent",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0, inWeight: 1 / 3f, outWeight: 1),
                new Keyframe(time: 200, value: 100, inTangent: 0, outTangent: 0, inWeight: 1, outWeight: 1 / 3f)
            );
            yield return new AnimationCurveCase("Normal Curve",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0)
            );
            yield return new AnimationCurveCase("Normal Curve With above 100 value",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 150, inTangent: 0, outTangent: 0)
            );
            yield return new AnimationCurveCase("Normal Curve With below 0 value",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 150, inTangent: 0, outTangent: 0)
            );
            yield return new AnimationCurveCase("Constant Segment: OutTangent = +infinity",
                new Keyframe(time: 0, value: -20, inTangent: 0, outTangent: float.PositiveInfinity),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0)
            );
            yield return new AnimationCurveCase("Constant Segment: OutTangent = -infinity",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: float.NegativeInfinity),
                new Keyframe(time: 1, value: 100, inTangent: 0, outTangent: 0)
            );
            yield return new AnimationCurveCase("Constant Segment: InTangent = +infinity",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.PositiveInfinity, outTangent: 0)
            );
            yield return new AnimationCurveCase("Constant Segment: InTangent = -infinity",
                new Keyframe(time: 0, value: 0, inTangent: 0, outTangent: 0),
                new Keyframe(time: 1, value: 100, inTangent: float.NegativeInfinity, outTangent: 0)
            );

#if MODULAR_AVATAR_FUZZ_TESTING
            // fuzz testing
            var random = new System.Random(Seed);
            for (int i = 0; i < 100; i++)
            {
                var inTangent = (float)(random.NextDouble() * 3000) - 1500;
                var outTangent = (float)(random.NextDouble() * 3000) - 1500;
                var leftWeight = (float)random.NextDouble();
                var rightWeight = (float)random.NextDouble();
                var inValue = (float)(random.NextDouble() * 300 - 100); // -100...200
                var outValue = (float)(random.NextDouble() * 300 - 100); // -100...200
                var time = random.Next(0, 6000) / 60f;
                inTangent /= time;
                outTangent /= time;
                var curveCase = new AnimationCurveCase(
                    $"Random Curve (seed: {Seed}, {i}) ({inTangent:G9}, {outTangent:G9}, {leftWeight:G9}, {rightWeight:G9}, {inValue:G9}, {outValue:G9}, {time:G9})",
                    new Keyframe(time: 0, value: inValue, inTangent: inTangent, outTangent: outTangent, inWeight: 0, outWeight: leftWeight),
                    new Keyframe(time: time, value: outValue, inTangent: outTangent, outTangent: inTangent, inWeight: rightWeight, outWeight: 0)
                );

                yield return curveCase;
            }
#endif
        }

        public static IEnumerable<AnimationCurveCase> TestRemapCases()
        {
            yield return new AnimationCurveCase("Linear-like with multiple split point",
                NewLinearFrame(0, 0),
                NewLinearFrame(1, 1),
                NewLinearFrame(20, 20),
                NewLinearFrame(50, 50),
                NewLinearFrame(90, 90),
                NewLinearFrame(100, 100)
            );
            yield return new AnimationCurveCase("Tangent Changing",
                NewLinearFrame(0, 0),
                NewLinearFrame(25, 0),
                NewLinearFrame(50, 100),
                NewLinearFrame(75, 0),
                NewLinearFrame(100, 0)
            );

#if MODULAR_AVATAR_FUZZ_TESTING
            // fuzz testing
            var random = new System.Random(Seed);
            for (int i = 0; i < 100; i++)
            {
                var additionalKeys = random.Next(0, 6);
                var frames = new List<Keyframe>();
                frames.Add(NewLinearFrame(0, (float)random.NextDouble()));
                frames.Add(NewLinearFrame(1, (float)random.NextDouble()));
                for (int j = 0; j < additionalKeys; j++)
                    frames.Add(NewLinearFrame((float)random.NextDouble(), (float)random.NextDouble()));
                frames.Sort((x, y) => x.time.CompareTo(y.time));

                var curveDescription = string.Join(", ", frames.Select(x => $"({x.time:G9}, {x.value:G9})"));

                var remapCase = new AnimationCurveCase($"Fuzzing Curve (seed: {Seed}, {i}) {curveDescription}", new AnimationCurve(frames.ToArray()));
                if (new RemapCurve(remapCase.Curve).SplitPoints.Any(x =>
                            Math.Abs(x.LeftDerivative) > 10 || Math.Abs(x.RightDerivative) > 10)
                    || Enumerable.Range(0, frames.Count - 1).Any(i => frames[i + 1].time - frames[i].time < 0.01)) 
                    continue; // it's too extreme to work well without problem of floating point precision

                yield return remapCase;
            }
#endif
        }

        [Test]
        [Combinatorial]
        public void TestRemapCurveIsCorrect(
            [ValueSource(nameof(TestCurves))] AnimationCurveCase aniCurveCase,
            [ValueSource(nameof(TestRemapCases))] AnimationCurveCase remapCase
        )
        {
            // This tests that remap is working as expect by comparing remapped curve and remap
            var animationCurve = aniCurveCase.Curve;
            var remapCurve = new RemapCurve(remapCase.Curve);
            var mappedCurve = BlendshapeSyncAnimationProcessor.MapCurve(animationCurve, remapCurve);
            Assert.That(remapCurve.IsIdentity, Is.False);

            var endTime = mappedCurve[mappedCurve.length - 1].time;

            for (float time = 0; time <= endTime; time += 0.01f)
            {
                var expectValue = remapCurve.GetPointOnCurve(animationCurve.Evaluate(time)).MappedValue;
                var mappedValue = mappedCurve.Evaluate(time);

                // we want to allow +/- 1% of error for value (in 0-100) and time (in seconds)
                // 1% in value 0-100 is 1.
                // 1% in time axis is 0.01 sec, and may result in error for 0.01 sec * tangent in value axis 
                var keyRange = Enumerable.Range(0, mappedCurve.length - 1).TakeWhile(i => mappedCurve[i].time <= time).Last();
                var maxTangent = Mathf.Max(Mathf.Abs(mappedCurve[keyRange].outTangent), Mathf.Abs(mappedCurve[keyRange + 1].inTangent));
                var allowedError = Mathf.Max(Mathf.Max(Mathf.Abs(expectValue) / 100, 1), maxTangent / 100);

                Assert.That(mappedValue, Is.EqualTo(expectValue).Within(allowedError), $"at {time}");
            }
        }

        public class AnimationCurveCase
        {
            private readonly string caseName;
            public AnimationCurve Curve;

            public AnimationCurveCase(string name, AnimationCurve curve) => (caseName, Curve) = (name, curve);
            public AnimationCurveCase(string name, params Keyframe[] keys) => (caseName, Curve) = (name, new AnimationCurve(keys));

            public override string ToString() => caseName;
        }

        #endregion

        #region Test Utilities

        private static Keyframe? linearReference;

        public static Keyframe NewLinearFrame(float time, float value)
        {
            if (linearReference is null)
            {
                var tmpCurve = new AnimationCurve(new Keyframe(0, 0));
                AnimationUtility.SetKeyLeftTangentMode(tmpCurve, 0, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(tmpCurve, 0, AnimationUtility.TangentMode.Linear);
                linearReference = tmpCurve[0];
            }

            var result = linearReference.Value;
            result.time = time;
            result.value = value;
            return result;
        }

        // to be used by debugger to save generated
        private static void SaveCurveForInvestigation(AnimationCurve original, AnimationCurve mapped)
        {
            var clip = new AnimationClip();
            AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding { type = typeof(SkinnedMeshRenderer), path = "", propertyName = "original" }, original);
            AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding { type = typeof(SkinnedMeshRenderer), path = "", propertyName = "mapped" }, mapped);
            AssetDatabase.CreateAsset(clip, "Assets/test.anim");
            AssetDatabase.SaveAssets();
        }

        #endregion
    }
}
