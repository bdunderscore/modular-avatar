using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct BlendshapeBinding
    {
        public AvatarObjectReference ReferenceMesh;
        public string Blendshape;
        public string LocalBlendshape;
        [ui.Curve(0, 0, 100, 100)]
        public AnimationCurve RemapCurve;

        public bool Equals(BlendshapeBinding other)
        {
            return Equals(ReferenceMesh, other.ReferenceMesh) && Blendshape == other.Blendshape;
        }

        public override bool Equals(object obj)
        {
            return obj is BlendshapeBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ReferenceMesh != null ? ReferenceMesh.GetHashCode() : 0) * 397) ^
                       (Blendshape != null ? Blendshape.GetHashCode() : 0);
            }
        }
    }

    internal static class BlendshapeSyncUpdateLoop
    {
        private static readonly HashSet<ModularAvatarBlendshapeSync> _syncs = new();

        static BlendshapeSyncUpdateLoop()
        {
            RuntimeUtil.OnUpdate += () =>
            {
                foreach (var bss in _syncs)
                {
                    if (bss != null)
                    {
                        bss.EditorUpdate();
                    }
                }
            };
            RuntimeUtil.OnHierarchyChanged += () =>
            {
                foreach (var bss in _syncs)
                {
                    if (bss != null && bss.gameObject != null && bss.isActiveAndEnabled)
                    {
                        bss.Rebind();
                    }
                }
            };
        }

        internal static void Register(ModularAvatarBlendshapeSync sync)
        {
            _syncs.Add(sync);
        }

        internal static void Unregister(ModularAvatarBlendshapeSync sync)
        {
            _syncs.Remove(sync);
        }
    }

    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Modular Avatar/MA Blendshape Sync")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/blendshape-sync?lang=auto")]
    public class ModularAvatarBlendshapeSync : AvatarTagComponent, IHaveObjReferences
    {
        public List<BlendshapeBinding> Bindings = new List<BlendshapeBinding>();

        internal struct EditorBlendshapeBinding
        {
            public SkinnedMeshRenderer TargetMesh;
            public int RemoteBlendshapeIndex;
            public int LocalBlendshapeIndex;
            public RemapCurve RemapCurve;
            public int BindingIndex;
        }

        internal List<EditorBlendshapeBinding> _editorBindings;

        protected override void OnValidate()
        {
            base.OnValidate();

            BlendshapeSyncUpdateLoop.Register(this);
            RuntimeUtil.delayCall(Rebind);
#if UNITY_EDITOR
            // limit the curve to linear curve since we cannot support non-liner curve reliably
            foreach (var blendshapeBinding in Bindings)
            {
                var remapCurve = blendshapeBinding.RemapCurve;
                if (remapCurve != null)
                {
                    if (remapCurve.length <= 1) continue;
                    // We ensure key at time = 0 and time = 100
                    if (remapCurve[0].time > 0) remapCurve.AddKey(0, remapCurve[0].value);
                    if (remapCurve[remapCurve.length - 1].time < 100) remapCurve.AddKey(100, remapCurve[remapCurve.length - 1].value);
                    for (int i = 0; i < remapCurve.length; i++)
                    {
                        UnityEditor.AnimationUtility.SetKeyBroken(remapCurve, i, true);
                        UnityEditor.AnimationUtility.SetKeyLeftTangentMode(remapCurve, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                        UnityEditor.AnimationUtility.SetKeyRightTangentMode(remapCurve, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                    }
                }
            }
#endif
        }

        private void Awake()
        {
            BlendshapeSyncUpdateLoop.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            BlendshapeSyncUpdateLoop.Unregister(this);
        }

        public override void ResolveReferences()
        {
            // no-op
        }

        internal void Rebind()
        {
            #if UNITY_EDITOR
            if (this == null) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;

            _editorBindings = new List<EditorBlendshapeBinding>();

            var localRenderer = GetComponent<SkinnedMeshRenderer>();
            var localMesh = localRenderer.sharedMesh;
            if (localMesh == null)
                return;

            foreach (var binding in Bindings)
            {
                var obj = binding.ReferenceMesh.Get(this);
                if (obj == null)
                    continue;
                var smr = obj.GetComponent<SkinnedMeshRenderer>();
                if (smr == null)
                    continue;
                var mesh = smr.sharedMesh;
                if (mesh == null)
                    continue;

                var localShape = string.IsNullOrWhiteSpace(binding.LocalBlendshape)
                    ? binding.Blendshape
                    : binding.LocalBlendshape;
                var localIndex = localMesh.GetBlendShapeIndex(localShape);
                var refIndex = mesh.GetBlendShapeIndex(binding.Blendshape);
                if (localIndex == -1 || refIndex == -1)
                    continue;

                _editorBindings.Add(new EditorBlendshapeBinding()
                {
                    TargetMesh = smr,
                    RemoteBlendshapeIndex = refIndex,
                    LocalBlendshapeIndex = localIndex,
                    RemapCurve = new RemapCurve(binding.RemapCurve),
                    BindingIndex = Bindings.IndexOf(binding)
                });
            }

            EditorUpdate();
            #endif
        }

        internal void EditorUpdate()
        {
            if (RuntimeUtil.isPlaying) return;

            if (_editorBindings == null) return;
            var localRenderer = GetComponent<SkinnedMeshRenderer>();
            if (localRenderer == null) return;
            foreach (var binding in _editorBindings)
            {
                if (binding.TargetMesh == null) continue;
                var weight = binding.TargetMesh.GetBlendShapeWeight(binding.RemoteBlendshapeIndex);
                weight = binding.RemapCurve.GetPointOnCurve(weight).MappedValue;
                var currentWeight = localRenderer.GetBlendShapeWeight(binding.LocalBlendshapeIndex);
                if (!Mathf.Approximately(currentWeight, weight))
                {
                    localRenderer.SetBlendShapeWeight(binding.LocalBlendshapeIndex, weight);
                }
            }
        }

        public IEnumerable<AvatarObjectReference> GetObjectReferences()
        {
            foreach (var binding in Bindings)
                if (binding.ReferenceMesh != null)
                    yield return binding.ReferenceMesh;
        }

#nullable enable
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
            private const int MapScale = 1;

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

            public bool IsIdentity => _originalValues.Length == 2 && _originalValues[0] == 0 && _mappedValues[0] == 0 && _mappedValues[1] == 100 && _originalValues[1] == 100;

            public IEnumerable<Point> SplitPoints => Enumerable.Range(1, _originalValues.Length - 2)
                .Select(i => new Point(this, i, _originalValues[i] * MapScale));

            public Point GetPointOnCurve(float value) => new(this, Array.BinarySearch(_originalValues, value / MapScale), value);

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

                public float MapTangent(double tangent, bool isOut) => !double.IsFinite(tangent) ? (float)tangent 
                    : (float)(tangent * (isOut == tangent < 0 ? LeftDerivative : RightDerivative));

                public double LeftDerivative => _binarySearch >= 0
                    ? _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(_binarySearch - 1))
                    : _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(~_binarySearch - 1));
                public double RightDerivative => _binarySearch >= 0
                    ? _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(_binarySearch))
                    : _remapCurve.DerivativeOfRange(_remapCurve.ClampRangeIndex(~_binarySearch - 1));
            }
        }
#nullable restore
    }
}