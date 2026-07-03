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
        private Dictionary<SummaryBinding, List<(SummaryBinding target, RemapCurve remapCurve)>> _bindingMappings;

        internal BlendshapeSyncAnimationProcessor(ndmf.BuildContext context)
        {
            _context = context;
            _bindingMappings = new Dictionary<SummaryBinding, List<(SummaryBinding target, RemapCurve remapCurve)>>();
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
            
            _bindingMappings = new Dictionary<SummaryBinding, List<(SummaryBinding target, RemapCurve remapCurve)>>();

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

                    var targetWeight = remapCurve.IsIdentity ? srcWeight : remapCurve.GetPointOnCurve(srcWeight).MappedValue;
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
                    dstBindings = new List<(SummaryBinding target, RemapCurve remapCurve)>();
                    _bindingMappings[srcBinding] = dstBindings;
                }

                var targetBlendshapeName = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;

                dstBindings.Add((new SummaryBinding(targetSmr, targetBlendshapeName), new RemapCurve(binding.RemapCurve)));
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
                AnimationUtility.SetKeyBroken(newCurve, i, true);
                AnimationUtility.SetKeyLeftTangentMode(newCurve, i, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(newCurve, i, AnimationUtility.TangentMode.Free);
            }

            return newCurve;
        }

        const float epsilon = 1 / 200f;

        internal static AnimationCurve MapCurve(AnimationCurve curve, RemapCurve remapCurve)
        {
            if (remapCurve.IsIdentity)
                return curve;

            // We expect the curve to be in Free mode.
            for (var i = 0; i < curve.length; i++)
            {
                Debug.Assert(AnimationUtility.GetKeyLeftTangentMode(curve, i) == AnimationUtility.TangentMode.Free);
                Debug.Assert(AnimationUtility.GetKeyLeftTangentMode(curve, i) == AnimationUtility.TangentMode.Free);
            }

            var outputKeyframes = new List<Keyframe>(curve.length);

            var nextKeyFromPrevLoop = curve[0];
            var nextKeyOnMapFromPrevLoop = remapCurve.GetPointOnCurve(nextKeyFromPrevLoop.value);
            // we loop for each keyframe range
            for (var rangeIndex = 0; rangeIndex < curve.length - 1; rangeIndex++)
            {
                var startKey = nextKeyFromPrevLoop;
                var startKeyOnMap = nextKeyOnMapFromPrevLoop;
                var endKey = curve[rangeIndex + 1];
                var endKeyOnMap = remapCurve.GetPointOnCurve(endKey.value);

                const float oneThird = 1.0f / 3;
                var startTangentWeight = (startKey.weightedMode & WeightedMode.Out) != 0 ? startKey.outWeight : oneThird;
                var endTangentWeight = (endKey.weightedMode & WeightedMode.In) != 0 ? endKey.inWeight : oneThird;
                var timeSpan = endKey.time - startKey.time;

                var timeAxisBezier = new BezierSegment(0, startTangentWeight, endTangentWeight, 1);
                var valueAxisBezier = new BezierSegment(startKey.value, 
                    startTangentWeight * startKey.outTangent * timeSpan, 
                    endTangentWeight * endKey.inTangent * timeSpan, 
                    endKey.value);

                startKey.value = startKeyOnMap.MappedValue;
                startKey.outTangent = startKeyOnMap.MapTangent(startKey.outTangent, isOut: true);

                var roots = new List<(double t, RemapCurve.SplitPoint splitPoint)>();

                // If tangent is infinite, the curve becomes constant curve which will never splits curve.
                if (float.IsFinite(startKey.outTangent) && float.IsFinite(endKey.inTangent))
                {
                    foreach (var splitPoint in remapCurve.SplitPoints)
                    {
                        var rootsForThisSplitPoint = valueAxisBezier.Solve(splitPoint.OriginalValue).ToArray();
                        foreach (var root in rootsForThisSplitPoint)
                        {
                            if (root is > epsilon and < (1 - epsilon))
                                roots.Add((root, splitPoint));
                        }
                    }
                }

                if (roots.Count != 0)
                {
                    // When the curve passes some of the split points, we split the curve at those points.

                    var isHermite = (startKey.weightedMode & WeightedMode.Out) == 0 && (endKey.weightedMode & WeightedMode.In) == 0;

                    roots.Add((0, default));
                    roots.Add((1, default));
                    roots.Sort((a, b) => a.t.CompareTo(b.t));

                    if (!isHermite)
                    {
                        startKey.weightedMode |= WeightedMode.Out;

                        var timeDerivative = timeAxisBezier.Derivative(0);

                        var tRangeAfter = roots[1].t - 0;
                        var timeRangeAfter = timeAxisBezier.Compute(roots[1].t) - timeAxisBezier.Compute(0);
                        startKey.outWeight = (float)(timeDerivative * tRangeAfter / timeRangeAfter / 3);
                    }

                    outputKeyframes.Add(startKey);

                    for (var i = 1; i < roots.Count - 1; i++)
                    {
                        Keyframe splitKey;
                        {
                            var rootT = roots[i].t;
                            var splitPoint = roots[i].splitPoint;

                            var timeAxisDerivative = isHermite ? 1 : timeAxisBezier.Derivative(rootT);
                            var valueAxisDerivative = valueAxisBezier.Derivative(rootT);
                            var tangent = valueAxisDerivative / timeAxisDerivative / timeSpan;

                            var insertTimeRatio = timeAxisBezier.Compute(rootT);

                            splitKey = new Keyframe
                            {
                                time = Mathf.LerpUnclamped(startKey.time, endKey.time, (float)insertTimeRatio),
                                value = (float)splitPoint.MappedValue,
                                inTangent = splitPoint.MapTangent(tangent, isOut: false),
                                outTangent = splitPoint.MapTangent(tangent, isOut: true),
                            };

                            if (!isHermite)
                            {
                                var timeDerivative = timeAxisBezier.Derivative(rootT);

                                var tRangeBefore = rootT - roots[i - 1].t;
                                var timeRangeBefore = insertTimeRatio - timeAxisBezier.Compute(roots[i - 1].t);
                                splitKey.inWeight = (float)(timeDerivative * tRangeBefore / timeRangeBefore / 3);

                                var tRangeAfter = roots[i + 1].t - rootT;
                                var timeRangeAfter = timeAxisBezier.Compute(roots[i + 1].t) - insertTimeRatio;
                                splitKey.outWeight = (float)(timeDerivative * tRangeAfter / timeRangeAfter / 3);

                                splitKey.weightedMode = WeightedMode.Both;
                            }
                        }

                        if (float.IsFinite(splitKey.inTangent) && float.IsFinite(splitKey.outTangent))
                        {
                            outputKeyframes.Add(splitKey);
                        }
                        else
                        {
                            // if either in/out is not finite, this typically means timeAxisDerivative ~= 0.
                            // to not create Infinite / NaN tangents
                            Keyframe leftSplitKey;
                            Keyframe rightSplitKey;

                            int multiplier = 0;
                            do
                            {
                                multiplier++;
                                var diff = epsilon * multiplier;
                                {
                                    var leftT = roots[i].t - diff;
                                    var splitPoint = roots[i].splitPoint;

                                    var timeAxisDerivative = isHermite ? 1 : timeAxisBezier.Derivative(leftT);
                                    var valueAxisDerivative = valueAxisBezier.Derivative(leftT);
                                    var tangent = valueAxisDerivative / timeAxisDerivative / timeSpan;

                                    var insertTimeRatio = timeAxisBezier.Compute(leftT);

                                    leftSplitKey = new Keyframe
                                    {
                                        time = Mathf.LerpUnclamped(startKey.time, endKey.time, (float)insertTimeRatio),
                                        value = (float)splitPoint.MappedValue,
                                        // this key is before the split point, so always use out tangent.
                                        inTangent = splitPoint.MapTangent(tangent, isOut: false),
                                        outTangent = splitPoint.MapTangent(tangent, isOut: false),
                                    };

                                    if (!isHermite)
                                    {
                                        var timeDerivative = timeAxisBezier.Derivative(leftT);

                                        var tRangeBefore = leftT - roots[i - 1].t;
                                        var timeRangeBefore = insertTimeRatio - timeAxisBezier.Compute(roots[i - 1].t);
                                        leftSplitKey.inWeight =
                                            (float)(timeDerivative * tRangeBefore / timeRangeBefore / 3);

                                        leftSplitKey.outWeight = oneThird;

                                        leftSplitKey.weightedMode = WeightedMode.In;
                                    }
                                }

                                {
                                    var rightT = roots[i].t + diff;
                                    var splitPoint = roots[i].splitPoint;

                                    var timeAxisDerivative = isHermite ? 1 : timeAxisBezier.Derivative(rightT);
                                    var valueAxisDerivative = valueAxisBezier.Derivative(rightT);
                                    var tangent = valueAxisDerivative / timeAxisDerivative / timeSpan;

                                    var insertTimeRatio = timeAxisBezier.Compute(rightT);

                                    rightSplitKey = new Keyframe
                                    {
                                        time = Mathf.LerpUnclamped(startKey.time, endKey.time, (float)insertTimeRatio),
                                        value = (float)splitPoint.MappedValue,
                                        // this key is after the split point, so always use in tangent.
                                        inTangent = splitPoint.MapTangent(tangent, isOut: true),
                                        outTangent = splitPoint.MapTangent(tangent, isOut: true),
                                    };

                                    if (!isHermite)
                                    {
                                        var timeDerivative = timeAxisBezier.Derivative(rightT);

                                        rightSplitKey.inWeight = oneThird;

                                        var tRangeAfter = roots[i + 1].t - rightT;
                                        var timeRangeAfter = timeAxisBezier.Compute(roots[i + 1].t) - insertTimeRatio;
                                        rightSplitKey.outWeight = (float)(timeDerivative * tRangeAfter / timeRangeAfter / 3);

                                        rightSplitKey.weightedMode = WeightedMode.Out;
                                    }
                                }
                            } while (!float.IsFinite(leftSplitKey.inTangent) || !float.IsFinite(rightSplitKey.outTangent));

                            outputKeyframes.Add(leftSplitKey);
                            outputKeyframes.Add(rightSplitKey);
                        }
                    }

                    if (!isHermite)
                    {
                        endKey.weightedMode |= WeightedMode.In;

                        var timeDerivative = timeAxisBezier.Derivative(1);

                        var tRangeBefore = 1 - roots[^2].t;
                        var timeRangeBefore = timeAxisBezier.Compute(1) - timeAxisBezier.Compute(roots[^2].t);
                        endKey.inWeight = (float)(timeDerivative * tRangeBefore / timeRangeBefore / 3);
                    }
                }
                else
                {
                    outputKeyframes.Add(startKey);
                }

                endKey.inTangent = endKeyOnMap.MapTangent(endKey.inTangent, isOut: false);

                nextKeyFromPrevLoop = endKey;
                nextKeyOnMapFromPrevLoop = endKeyOnMap;
            }

            nextKeyFromPrevLoop.value = nextKeyOnMapFromPrevLoop.MappedValue;
            outputKeyframes.Add(nextKeyFromPrevLoop);

            var newCurve = new AnimationCurve(outputKeyframes.ToArray())
            {
                preWrapMode = curve.preWrapMode,
                postWrapMode = curve.postWrapMode,
            };

            for (var index = 0; index < newCurve.length; index++)
                AnimationUtility.SetKeyBroken(newCurve, index, true);

            return newCurve;
        }

        /// <summary>
        /// The class folds information of remapCurve.
        ///
        /// This class provides easy access to the remap curve of BlendShape Sync.
        ///
        /// The reason why this class is used instead of directly using Animation Curve is:
        /// - We want to use Array.BinarySearch to find the index of the closest remap key or segment. There is no generic way to do this with Animation Curve in C# api.
        /// - We want to get derivative of the remap curve easily. We often require this for mapping implementation.
        /// - We want to change the behavior of the remap curve for out-of-range values for compatibility reasons.
        ///   In previous versions of Modular Avatar without remap curves, any out-of-zero range will be mapped as-is.
        ///   However, AnimationCurve will repeat or clamp the out-of-range values.
        ///   We need to preserve previous behavior for remap curves looks like identity, so we need to change the behavior for out-of-range values.
        /// </summary>
        internal class RemapCurve
        {
            private const int MapScale = 100;
            private static readonly FloatComparerIgnoreEpsilon Comparer = new(epsilon);

            private readonly float[] _originalValues;
            private readonly float[] _mappedValues;

            public RemapCurve(AnimationCurve? curve)
            {
                if (curve == null || curve.length < 2)
                {
                    _originalValues = new float[] { 0, 1 };
                    _mappedValues = new float[] { 0, 1 };
                }
                else
                {
                    _originalValues = curve.keys.Select(k => k.time).ToArray();
                    _mappedValues = curve.keys.Select(k => k.value).ToArray();
                }
            }

            public bool IsIdentity => _originalValues.Length == 2 && _originalValues[0] == 0 && _mappedValues[0] == 0 && _mappedValues[1] == 1 && _originalValues[1] == 1;

            public IEnumerable<SplitPoint> SplitPoints => Enumerable.Range(1, _originalValues.Length - 2).Select(i => new SplitPoint(this, i));

            public Point GetPointOnCurve(float value) => new(this, Array.BinarySearch(_originalValues, value / MapScale, Comparer), value);

            private double DerivativeOfRange(int range)
                => ((double)_mappedValues[range + 1] - _mappedValues[range])
                   / ((double)_originalValues[range + 1] - _originalValues[range]);

            private int ClampRangeIndex(int range) => Mathf.Clamp(range, 0, _originalValues.Length - 2);

            private double GetMappedValue(int binarySearch, float value)
            {
                if (binarySearch >= 0)
                {
                    // if the value is exactly at 
                    return _mappedValues[binarySearch] * MapScale;
                }
                else
                {
                    var range = ClampRangeIndex(~binarySearch - 1);
                    return (_mappedValues[range] + DerivativeOfRange(range) * (value / MapScale - _originalValues[range])) * MapScale;
                }
            }

            public readonly struct Point
            {
                private readonly RemapCurve _remapCurve;
                private readonly int _binarySearch;
                private readonly float _originalValue;

                internal Point(RemapCurve remapCurve, int binarySearch, float originalValue)
                    => (_remapCurve, _binarySearch, _originalValue) = (remapCurve, binarySearch, originalValue);

                public float OriginalValue => _originalValue;
                public float MappedValue => (float)_remapCurve.GetMappedValue(_binarySearch, _originalValue);

                public float MapTangent(float tangent, bool isOut) => !float.IsFinite(tangent) ? tangent 
                    : (float)(tangent * (isOut == tangent < 0 ? LeftDerivative : RightDerivative));

                public double LeftDerivative => _binarySearch >= 0
                    ? _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(_binarySearch - 1))
                    : _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(~_binarySearch - 1));
                public double RightDerivative => _binarySearch >= 0
                    ? _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(_binarySearch))
                    : _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(~_binarySearch - 1));
            }

            public readonly struct SplitPoint
            {
                private readonly RemapCurve _remapCurve;
                private readonly int _index;

                internal SplitPoint(RemapCurve remapCurve, int i) => (_remapCurve, _index) = (remapCurve, i);

                public double OriginalValue => (double)_remapCurve._originalValues[_index] * MapScale;
                public double MappedValue => _remapCurve._mappedValues[_index] * MapScale;
                
                public float MapTangent(double tangent, bool isOut) => !double.IsFinite(tangent) ? (float)tangent 
                    : (float)(tangent * (isOut == tangent < 0 ? LeftDerivative : RightDerivative));

                public double LeftDerivative => _remapCurve.DerivativeOfRange(_index - 1);
                public double RightDerivative => _remapCurve.DerivativeOfRange(_index);
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

            public double[] Solve(double value) => CubicSolver.SolveCubicDouble(a, b, c, d - value);
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
