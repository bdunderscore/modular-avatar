#nullable enable
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public abstract class AbstractMeshDeformComponent : AvatarTagComponent
    {
        internal AbstractMeshDeformComponent()
        {
            // prevent subclassing
        }

        [SerializeField] [FormerlySerializedAs("Target")]
        private AvatarObjectReference _target = new();

        public AvatarObjectReference Target
        {
            get => _target;
            set => _target = value;
        }

        internal static Action<AbstractMeshDeformComponent>? OnGizmosCallback;

        internal IDisposable? CachedGizmoHandle;

        private void OnDrawGizmosSelected()
        {
            OnGizmosCallback?.Invoke(this);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            CachedGizmoHandle?.Dispose();
            CachedGizmoHandle = null;
        }
    }
}