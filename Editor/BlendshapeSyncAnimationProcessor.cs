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
    using RemapCurve = ModularAvatarBlendshapeSync.RemapCurve;

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

        const float epsilon = 1 / 500f;

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

                var roots = new List<(double t, RemapCurve.Point splitPoint)>();

                // If tangent is infinite, the curve becomes constant curve which will never splits curve.
                if (float.IsFinite(startKey.outTangent) && float.IsFinite(endKey.inTangent))
                {
                    foreach (var splitPoint in remapCurve.SplitPoints)
                    {
                        var rootsForThisSplitPoint = valueAxisBezier.Solve(splitPoint.OriginalValue).ToArray();
                        foreach (var root in rootsForThisSplitPoint)
                        {
                             if (root is > 0 and < 1)
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
                        startKey.outWeight = ComputeWeight(0, roots[1].t);
                    }

                    outputKeyframes.Add(startKey);

                    // This is necessary because we might have keyframe not exactly at roots[i - 1].t when tangent is infinity
                    double lastKeyT = 0;
                    for (var i = 1; i < roots.Count - 1; i++)
                    {
                        var splitPoint = roots[i].splitPoint;

                        Keyframe splitKey;
                        {
                            var rootT = roots[i].t;

                            splitKey = new Keyframe
                            {
                                time = Mathf.LerpUnclamped(startKey.time, endKey.time, (float)timeAxisBezier.Compute(rootT)),
                                value = splitPoint.MappedValue,
                                inTangent = splitPoint.MapTangent(ComputeTangent(rootT), isOut: false),
                                outTangent = splitPoint.MapTangent(ComputeTangent(rootT), isOut: true),
                            };

                            if (!isHermite)
                            {
                                splitKey.inWeight = ComputeWeight(rootT, lastKeyT);
                                splitKey.outWeight = ComputeWeight(rootT, roots[i + 1].t);
                                splitKey.weightedMode = WeightedMode.Both;
                            }
                        }

                        if (float.IsFinite(splitKey.inTangent) && float.IsFinite(splitKey.outTangent))
                        {
                            lastKeyT = roots[i].t;
                            outputKeyframes.Add(splitKey);
                        }
                        else
                        {
                            // if either in/out is not finite, this typically means timeAxisDerivative ~= 0.
                            // to not create Infinite / NaN tangents
                            Keyframe leftSplitKey;
                            Keyframe rightSplitKey;

                            var multiplier = 0;
                            float diff;
                            double leftT;
                            double rightT;
                            do
                            {
                                multiplier++;
                                diff = epsilon * multiplier;
 
                                leftT = roots[i].t - diff;
                                rightT = roots[i].t + diff;

                                if (leftT <= lastKeyT || rightT > roots[i + 1].t) throw new Exception("Failed to split keyframe");

                                leftSplitKey = new Keyframe
                                {
                                    time = Mathf.LerpUnclamped(startKey.time, endKey.time, (float)timeAxisBezier.Compute(leftT)),
                                    value = remapCurve.GetPointOnCurve((float)valueAxisBezier.Compute(leftT)).MappedValue,
                                    // this key is before the split point, so always use out tangent.
                                    inTangent = splitPoint.MapTangent(ComputeTangent(leftT), isOut: false),
                                    outTangent = splitPoint.MapTangent(ComputeTangent(leftT), isOut: false),
                                };

                                rightSplitKey = new Keyframe
                                {
                                    time = Mathf.LerpUnclamped(startKey.time, endKey.time, (float)timeAxisBezier.Compute(rightT)),
                                    value = remapCurve.GetPointOnCurve((float)valueAxisBezier.Compute(rightT)).MappedValue,
                                    // this key is after the split point, so always use in tangent.
                                    inTangent = splitPoint.MapTangent(ComputeTangent(rightT), isOut: true),
                                    outTangent = splitPoint.MapTangent(ComputeTangent(rightT), isOut: true),
                                };

                                if (!isHermite)
                                {
                                    leftSplitKey.inWeight = ComputeWeight(leftT, lastKeyT);
                                    leftSplitKey.outWeight = oneThird;
                                    leftSplitKey.weightedMode = WeightedMode.In;
                                
                                    rightSplitKey.inWeight = oneThird;
                                    rightSplitKey.outWeight = ComputeWeight(rightT, roots[i + 1].t);
                                    rightSplitKey.weightedMode = WeightedMode.Out;
                                }
                            } while (!float.IsFinite(leftSplitKey.inTangent) || !float.IsFinite(rightSplitKey.outTangent) || rightSplitKey.time - leftSplitKey.time <= 0);

                            {
                                // The outWeight of last keyframe is computed based on assumption that
                                // next key will be placed exactly at roots[i].t but we have moved a little
                                // so we need to fix it

                                var lastKey = outputKeyframes[^1];
                                lastKey.outWeight = ComputeWeight(roots[i - 1].t, leftT);
                                outputKeyframes[^1] = lastKey;
                            }

                            lastKeyT = rightT;
                            outputKeyframes.Add(leftSplitKey);
                            outputKeyframes.Add(rightSplitKey);
                        }
                    }

                    if (!isHermite)
                    {
                        endKey.weightedMode |= WeightedMode.In;
                        endKey.inWeight = ComputeWeight(1, lastKeyT);
                    }

                    double ComputeTangent(double t)
                    {
                        var timeAxisDerivative = isHermite ? 1 : timeAxisBezier.Derivative(t);
                        var valueAxisDerivative = valueAxisBezier.Derivative(t);
                        return valueAxisDerivative / timeAxisDerivative / timeSpan;
                    }

                    float ComputeWeight(double ourT, double siblingT)
                    {
                        var timeDerivative = timeAxisBezier.Derivative(ourT);
                        var tRange = ourT - siblingT;
                        var timeRange = timeAxisBezier.Compute(ourT) - timeAxisBezier.Compute(siblingT);
                        return (float)(timeDerivative * tRange / timeRange / 3);
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
        /// Represents single-axis single bezier curve.
        /// 
        /// If this is only used on value axis, it will be hermite curve.
        /// </summary>
        public readonly struct BezierSegment
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

            public IEnumerable<float> Solve(double value)
            {
                return CubicSolver.SolveCubicInterval(a, b, c, d - value, 0.0, 1.0);
            }
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
