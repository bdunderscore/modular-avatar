#if MA_VRCSDK3_AVATARS

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    /**
     * Ensures that any blendshapes marked for syncing by BlendshapeSync propagate values in all animation clips.
     *
     * Note that we only look at the FX layer, as any other layer won't work properly with mirror reflections anyway.
     */
    internal class BlendshapeSyncAnimationProcessor
    {
        private readonly ndmf.BuildContext _context;
        private Dictionary<SummaryBinding, List<(SummaryBinding target, AnimationCurve remapCurve)>> _bindingMappings;

        internal BlendshapeSyncAnimationProcessor(ndmf.BuildContext context)
        {
            _context = context;
            _bindingMappings = new Dictionary<SummaryBinding, List<(SummaryBinding target, AnimationCurve remapCurve)>>();
        }

        private struct SummaryBinding : IEquatable<SummaryBinding>
        {
            private const string PREFIX = "blendShape.";
            public readonly SkinnedMeshRenderer Renderer;
            public string propertyName => PREFIX + BlendshapeName;
            public readonly string BlendshapeName;

            public SummaryBinding(SkinnedMeshRenderer renderer, string blendShape)
            {
                Renderer = renderer;
                BlendshapeName = blendShape;
            }

            public static SummaryBinding? FromEditorBinding(AnimatorServicesContext asc, EditorCurveBinding binding)
            {
                if (binding.type != typeof(SkinnedMeshRenderer) || !binding.propertyName.StartsWith(PREFIX))
                {
                    return null;
                }

                var obj = asc.ObjectPathRemapper.GetObjectForPath(binding.path);
                if (obj == null || !obj.TryGetComponent<SkinnedMeshRenderer>(out var smr)) return null;

                return new SummaryBinding(smr, binding.propertyName.Substring(PREFIX.Length));
            }

            public EditorCurveBinding ToEditorCurveBinding(AnimatorServicesContext asc)
            {
                return EditorCurveBinding.FloatCurve(
                    asc.ObjectPathRemapper.GetVirtualPathForObject(Renderer.gameObject),
                    typeof(SkinnedMeshRenderer),
                    propertyName
                );
            }

            public IEnumerable<EditorCurveBinding> ToSourceEditorCurveBindings(AnimatorServicesContext asc)
            {
                foreach (var path in asc.ObjectPathRemapper.GetAllPathsForObject(Renderer.gameObject))
                {
                    yield return EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), propertyName);
                }
            }

            public bool Equals(SummaryBinding other)
            {
                return Renderer == other.Renderer && propertyName == other.propertyName;
            }

            public override bool Equals(object? obj)
            {
                return obj is SummaryBinding other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Renderer, propertyName);
            }
        }

        public void OnPreprocessAvatar()
        {
            var avatarGameObject = _context.AvatarRootObject;
            var asc = _context.Extension<AnimatorServicesContext>();
            var animDb = asc.AnimationIndex;
            
            _bindingMappings = new Dictionary<SummaryBinding, List<(SummaryBinding target, AnimationCurve remapCurve)>>();

            var components = avatarGameObject.GetComponentsInChildren<ModularAvatarBlendshapeSync>(true);
            if (components.Length == 0) return;

            foreach (var component in components)
            {
                BuildReport.ReportingObject(component, () => ProcessComponent(avatarGameObject, component));
            }

            // Apply the initial state of each binding to its targets
            foreach (var (source, targets) in _bindingMappings)
            {
                var smr = source.Renderer;
                if (smr == null) continue;

                var srcIndex = smr.sharedMesh.GetBlendShapeIndex(source.BlendshapeName);
                if (srcIndex < 0) continue;

                var srcWeight = smr.GetBlendShapeWeight(srcIndex);

                foreach (var (target, remapCurve) in targets)
                {
                    var targetSmr = target.Renderer;
                    if (targetSmr == null) continue;

                    var targetIndex = targetSmr.sharedMesh.GetBlendShapeIndex(target.BlendshapeName);
                    if (targetIndex < 0) continue;

                    var targetWeight = (remapCurve != null && remapCurve.length >= 2)
                        ? remapCurve.Evaluate(srcWeight / 100f) * 100f
                        : srcWeight;
                    targetSmr.SetBlendShapeWeight(targetIndex, targetWeight);
                }
            }

            var clips = new HashSet<VirtualClip>();
            foreach (var key in _bindingMappings.Keys)
            {
                foreach (var ecb in key.ToSourceEditorCurveBindings(asc))
                {
                    clips.UnionWith(animDb.GetClipsForBinding(ecb));
                }
            }

            // Walk and transform all clips
            foreach (var clip in clips)
            {
                ProcessClip(asc, clip);
            }
        }

        private void ProcessComponent(GameObject avatarGameObject, ModularAvatarBlendshapeSync component)
        {
            var targetSmr = component.gameObject.GetComponent<SkinnedMeshRenderer>();
            if (targetSmr == null) return;

            foreach (var binding in component.Bindings)
            {
                var refObj = binding.ReferenceMesh.Get(component);
                if (refObj == null) continue;
                var refSmr = refObj.GetComponent<SkinnedMeshRenderer>();
                if (refSmr == null) continue;

                var srcBinding = new SummaryBinding(refSmr, binding.Blendshape);

                if (!_bindingMappings.TryGetValue(srcBinding, out var dstBindings))
                {
                    dstBindings = new List<(SummaryBinding target, AnimationCurve remapCurve)>();
                    _bindingMappings[srcBinding] = dstBindings;
                }

                var targetBlendshapeName = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;

                dstBindings.Add((new SummaryBinding(targetSmr, targetBlendshapeName), binding.RemapCurve));
            }
        }

        private void ProcessClip(AnimatorServicesContext asc, VirtualClip clip)
        {
            foreach (var binding in clip.GetFloatCurveBindings().ToList())
            {
                var srcBinding = SummaryBinding.FromEditorBinding(asc, binding);
                if (srcBinding == null || !_bindingMappings.TryGetValue(srcBinding.Value, out var dstBindings))
                {
                    continue;
                }

                var curve = clip.GetFloatCurve(binding)!;
                foreach (var (dst, remapCurve) in dstBindings)
                {
                    clip.SetFloatCurve(dst.ToEditorCurveBinding(asc), MapCurve(NormalizeCurveToFreeTangents(curve), remapCurve));
                }
            }
        }

        internal static AnimationCurve NormalizeCurveToFreeTangents(AnimationCurve curve)
        {
            var newCurve = new AnimationCurve(curve.keys)
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode
            };

            for (var i = 0; i < newCurve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(newCurve, i, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(newCurve, i, AnimationUtility.TangentMode.Free);
            }

            return newCurve;
        }

        internal static AnimationCurve MapCurve(AnimationCurve curve, AnimationCurve? remapCurve)
        {
            if (remapCurve == null || remapCurve.length < 2 || 
                remapCurve.length == 2 
                && remapCurve[0].time == 0 && remapCurve[0].value == 0 
                && remapCurve[1].time == 1 && remapCurve[1].value == 1)
                return curve;

            // the remapCurve, splitPoints, and derivatives
            //
            //   x
            //   ^
            //   | keys[0]              keys[2]                 so keys.Length == 4
            //   |/        keys[1]                    keys[3]
            // 1 +------------*------------+------------*
            //   |          ,'|',          |          ,'|
            //   |        ,'  |  ',        |        ,'  |
            //   |      ,'    |    ',      |      ,'    |
            //   |    ,'      |      ',    |    ,'      |
            //   |  ,'        |        ',  |  ,'        |
            //   |,'          |          ',|,'          |
            // 0 *------------+------------*------------*--+> t
            //   0      splitPoints[0]     |            1
            //   |            |      splitPoints[1]     |
            //   |============|============|============|
            //   derivatives[0]            derivatives[2]
            //                derivatives[1]

            // the list of original curve values that (may) changes their derivatives
            var splitPoints = new float[remapCurve.length - 2];
            for (var i = 0; i < splitPoints.Length; i++)
                splitPoints[i] = remapCurve[i + 1].time;
            var splitPointValues = new float[remapCurve.length - 2];
            for (var i = 0; i < splitPoints.Length; i++)
                splitPointValues[i] = remapCurve[i + 1].value;
            var derivatives = new double[remapCurve.length - 1];
            for (var i = 0; i < derivatives.Length; i++)
                derivatives[i] = ((double)remapCurve[i + 1].value - remapCurve[i].value) / ((double)remapCurve[i + 1].time - remapCurve[i].time);

            const float epsilon = 0.005f; // ~200fps
            var comparer = new FloatComparerIgnoreEpsilon(epsilon);

            var outputKeyframes = new List<FullKeyframe>(curve.length);

            var nextKeyFromPrevLoop = new FullKeyframe(curve, 0);
            for (var index = 0; index < curve.length; index++)
            {
                var originalKey = nextKeyFromPrevLoop;
                var key = originalKey;

                Debug.Assert(originalKey.RightTangentMode == AnimationUtility.TangentMode.Free);
                Debug.Assert(originalKey.LeftTangentMode == AnimationUtility.TangentMode.Free);

                var find = Array.BinarySearch(splitPoints, key.Keyframe.value / 100f, comparer);
                var val = remapCurve.Evaluate(key.Keyframe.value / 100f) * 100f;
                key.Broken = true;
                if (find >= 0)
                {
                    var pointIndex = find;
                    // the point is exactly at splitPoints[pointIndex].

                    var slopeLeft = derivatives[pointIndex];
                    var slopeRight = derivatives[pointIndex + 1];

                    key.Keyframe.value = val;
                    key.Keyframe.inTangent = (float)(key.Keyframe.inTangent * (key.Keyframe.inTangent > 0 ? slopeLeft : slopeRight));
                    key.Keyframe.outTangent = (float)(key.Keyframe.outTangent * (key.Keyframe.outTangent < 0 ? slopeLeft : slopeRight));
                }
                else
                {
                    var derivativeRangeIndex = ~find;
                    // the point is in derivatives[derivativeRangeIndex] range

                    var slope = derivatives[derivativeRangeIndex];

                    key.Keyframe.value = val;
                    key.Keyframe.inTangent = (float)(key.Keyframe.inTangent * slope);
                    key.Keyframe.outTangent = (float)(key.Keyframe.outTangent * slope);
                }

                if (index + 1 < curve.length)
                {
                    var nextKeyIndex = index + 1;
                    var nextOriginalKey = new FullKeyframe(curve, nextKeyIndex);
                    var nextKey = nextOriginalKey;

                    const float oneThird = 1.0f / 3;

                    var startTangentWeight = (originalKey.Keyframe.weightedMode & WeightedMode.Out) != 0 ? originalKey.Keyframe.outWeight : oneThird;
                    var endTangentWeight = (nextOriginalKey.Keyframe.weightedMode & WeightedMode.In) != 0 ? nextOriginalKey.Keyframe.inWeight : oneThird;
                    var timeSpan = nextOriginalKey.Keyframe.time - originalKey.Keyframe.time;

                    var timeAxisBezier = new BezierSegment(0, startTangentWeight, endTangentWeight, 1);
                    var valueAxisBezier = new BezierSegment(originalKey.Keyframe.value, 
                        startTangentWeight * originalKey.Keyframe.outTangent * timeSpan, 
                        endTangentWeight * nextOriginalKey.Keyframe.inTangent * timeSpan, 
                        nextOriginalKey.Keyframe.value);

                    var roots = new List<(double t, int pointIndex)>();

                    // If tangent is infinite, the curve becomes constant curve which will never splits curve.
                    if (float.IsFinite(originalKey.Keyframe.outTangent) &&
                        float.IsFinite(nextOriginalKey.Keyframe.inTangent))
                    {
                        for (var i = 0; i < splitPoints.Length; i++)
                        {
                            var splitPoint = splitPoints[i];
                            var rootsForThisSplitPoint = valueAxisBezier.Solve(splitPoint * 100).ToArray();
                            foreach (var root in rootsForThisSplitPoint)
                            {
                                if (root is > epsilon and < (1 - epsilon))
                                    roots.Add((root, i));
                            }
                        }
                    }

                    if (roots.Count != 0)
                    {
                        // When the curve passes some of the split points, we split the curve at those points.

                        var isHermite = (originalKey.Keyframe.weightedMode & WeightedMode.Out) == 0 && (nextOriginalKey.Keyframe.weightedMode & WeightedMode.In) == 0;

                        roots.Add((0, -1));
                        roots.Add((1, -1));
                        roots.Sort((a, b) => a.t.CompareTo(b.t));

                        if (!isHermite)
                        {
                            key.Keyframe.weightedMode |= WeightedMode.Out;

                            var timeDerivative = timeAxisBezier.Derivative(0);

                            var tRangeAfter = roots[1].t - 0;
                            var timeRangeAfter = timeAxisBezier.Compute(roots[1].t) - timeAxisBezier.Compute(0);
                            key.Keyframe.outWeight = (float)(timeDerivative * tRangeAfter / timeRangeAfter / 3);
                        }

                        outputKeyframes.Add(key);

                        for (var i = 1; i < roots.Count - 1; i++)
                        {
                            var root = roots[i];

                            // TODO: We need to special handle timeAxisDerivative == 0 since it will create infinite tangent
                            var timeAxisDerivative = isHermite ? 1 : timeAxisBezier.Derivative(root.t);
                            var valueAxisDerivative = valueAxisBezier.Derivative(root.t);
                            var tangent = valueAxisDerivative / timeAxisDerivative / timeSpan;

                            var insertTimeRatio = timeAxisBezier.Compute(root.t);

                            var newFrame0 = new FullKeyframe
                            {
                                Broken = true,
                                LeftTangentMode = AnimationUtility.TangentMode.Free,
                                RightTangentMode = AnimationUtility.TangentMode.Free,
                                Keyframe = {
                                    time = Mathf.LerpUnclamped(originalKey.Keyframe.time, nextOriginalKey.Keyframe.time, (float)insertTimeRatio),
                                    value = splitPointValues[root.pointIndex] * 100,
                                    inTangent = (float)(tangent * (tangent > 0 ? derivatives[root.pointIndex] : derivatives[root.pointIndex + 1])),
                                    outTangent = (float)(tangent * (tangent > 0 ? derivatives[root.pointIndex + 1] : derivatives[root.pointIndex])),
                                },
                            };

                            if (!isHermite)
                            {
                                var timeDerivative = timeAxisBezier.Derivative(root.t);

                                var tRangeBefore = root.t - roots[i - 1].t;
                                var timeRangeBefore = insertTimeRatio - timeAxisBezier.Compute(roots[i - 1].t);
                                newFrame0.Keyframe.inWeight = (float)(timeDerivative * tRangeBefore / timeRangeBefore / 3);

                                var tRangeAfter = roots[i + 1].t - root.t;
                                var timeRangeAfter = timeAxisBezier.Compute(roots[i + 1].t) - insertTimeRatio;
                                newFrame0.Keyframe.outWeight = (float)(timeDerivative * tRangeAfter / timeRangeAfter / 3);

                                newFrame0.Keyframe.weightedMode = WeightedMode.Both;
                            }

                            outputKeyframes.Add(newFrame0);
                        }

                        if (!isHermite)
                        {
                            nextKey.Keyframe.weightedMode |= WeightedMode.In;

                            var timeDerivative = timeAxisBezier.Derivative(1);

                            var tRangeBefore = 1 - roots[^2].t;
                            var timeRangeBefore = timeAxisBezier.Compute(1) - timeAxisBezier.Compute(roots[^2].t);
                            nextKey.Keyframe.inWeight = (float)(timeDerivative * tRangeBefore / timeRangeBefore / 3);
                        }
                    }
                    else
                    {
                        outputKeyframes.Add(key);
                    }

                    nextKeyFromPrevLoop = nextKey;
                }
                else
                {
                    outputKeyframes.Add(key);
                }
            }

            var keys = new Keyframe[outputKeyframes.Count];

            for (var i = 0; i < outputKeyframes.Count; i++)
                keys[i] = outputKeyframes[i].Keyframe;

            var newCurve = new AnimationCurve(keys);

            for (var index = 0; index < outputKeyframes.Count; index++)
            {
                AnimationUtility.SetKeyBroken(newCurve, index, outputKeyframes[index].Broken);
                AnimationUtility.SetKeyLeftTangentMode(newCurve, index, outputKeyframes[index].LeftTangentMode);
                AnimationUtility.SetKeyRightTangentMode(newCurve, index, outputKeyframes[index].RightTangentMode);
            }

            return newCurve;
        }

        /// <summary>
        /// The struct that holds keyframe information with TangentMode and Broken state.
        ///
        /// (The TangentMode and Broken state are stored in Keyframe struct and accessible with internal function, but unaccessible through public API)
        /// </summary>
        private struct FullKeyframe
        {
            public Keyframe Keyframe;
            public AnimationUtility.TangentMode LeftTangentMode;
            public AnimationUtility.TangentMode RightTangentMode;
            public bool Broken;

            public FullKeyframe(AnimationCurve curve, int index)
            {
                var keyIn = curve[index];
                // We do not copy tangentMode because setting tangentMode may let unity alter Keyframe on SetKey{Left,Right}TangentMode
                Keyframe = new Keyframe
                {
                    time = keyIn.time,
                    value = keyIn.value,
                    inTangent = keyIn.inTangent,
                    outTangent = keyIn.outTangent,
                    inWeight = keyIn.inWeight,
                    outWeight = keyIn.outWeight,
                    weightedMode = keyIn.weightedMode,
                };
                LeftTangentMode = AnimationUtility.GetKeyLeftTangentMode(curve, index);
                RightTangentMode = AnimationUtility.GetKeyRightTangentMode(curve, index);
                Broken = AnimationUtility.GetKeyBroken(curve, index);
            }
        }

        /// <summary>
        /// Represents single-axis single bezier curve.
        /// 
        /// If this is only used on value axis, it will be hermite curve.
        /// </summary>
        readonly struct BezierSegment
        {
            // bezier curve in a + bt + ct^2 + dt^3
            // bezier curve in at^3 + bt^2 + ct + d
            private readonly double a;
            private readonly double b;
            private readonly double c;
            private readonly double d;

            public BezierSegment(float p0, float d1, float d2, float p3)
            {
                a = 2.0 * p0 + 3.0 * d1 + 3.0 * d2 - 2.0 * p3;
                b = -3.0 * p0 - 6.0 * d1 - 3.0 * d2 + 3.0 * p3;
                c = 3.0 * d1;
                d = p0;
            }

            public double Derivative(double t) => 3.0 * a * t * t + 2.0 * b * t + c;

            public double Compute(double t) => a * t * t * t + b * t * t + c * t + d;

            public CubicSolver.Roots Solve(float value) => CubicSolver.SolveCubicDouble(a, b, c, d - value);
        }
    }

    // please note that this comparator is not transitive in equality
    internal class FloatComparerIgnoreEpsilon : IComparer<float>
    {
        private readonly float _epsilon;

        public FloatComparerIgnoreEpsilon(float epsilon) => _epsilon = epsilon;

        public int Compare(float x, float y) => Mathf.Abs(x - y) < _epsilon ? 0 : x.CompareTo(y);
    }
}

#endif
