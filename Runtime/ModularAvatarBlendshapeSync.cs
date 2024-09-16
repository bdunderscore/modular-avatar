using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct BlendshapeBinding
    {
        public AvatarObjectReference ReferenceMesh;
        public string Blendshape;
        public string LocalBlendshape;

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

    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Modular Avatar/MA Blendshape Sync")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/blendshape-sync?lang=auto")]
    public class ModularAvatarBlendshapeSync : AvatarTagComponent, IHaveObjReferences
    {
        public List<BlendshapeBinding> Bindings = new List<BlendshapeBinding>();

        struct EditorBlendshapeBinding
        {
            public SkinnedMeshRenderer TargetMesh;
            public int RemoteBlendshapeIndex;
            public int LocalBlendshapeIndex;
        }

        private List<EditorBlendshapeBinding> _editorBindings;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (RuntimeUtil.isPlaying) return;
            RuntimeUtil.delayCall(Rebind);
            RuntimeUtil.OnHierarchyChanged -= Rebind;
            RuntimeUtil.OnHierarchyChanged += Rebind;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RuntimeUtil.OnHierarchyChanged -= Rebind;
        }

        public override void ResolveReferences()
        {
            // no-op
        }

        private void Rebind()
        {
            #if UNITY_EDITOR
            if (this == null) return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;

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
                    LocalBlendshapeIndex = localIndex
                });
            }

            Update();
            #endif
        }

        private void Update()
        {
            if (RuntimeUtil.isPlaying) return;

            if (_editorBindings == null) return;
            var localRenderer = GetComponent<SkinnedMeshRenderer>();
            if (localRenderer == null) return;
            foreach (var binding in _editorBindings)
            {
                if (binding.TargetMesh == null) return;
                var weight = binding.TargetMesh.GetBlendShapeWeight(binding.RemoteBlendshapeIndex);
                localRenderer.SetBlendShapeWeight(binding.LocalBlendshapeIndex, weight);
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