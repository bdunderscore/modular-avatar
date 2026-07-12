using System;
using System.Collections.Generic;
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
                var remapCurve = Bindings[binding.BindingIndex].RemapCurve;
                if (remapCurve != null && remapCurve.length >= 2)
                    weight = remapCurve.Evaluate(weight);
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
    }
}