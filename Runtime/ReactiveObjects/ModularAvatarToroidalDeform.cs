#nullable enable

using System;

namespace nadena.dev.modular_avatar.core
{
    // Tag interface
    public interface IMeshDeformComponent
    {
        public AvatarObjectReference Target { get; set; }
    }

    public class ModularAvatarToroidalDeform : AvatarTagComponent, IMeshDeformComponent
    {
        internal static Action<ModularAvatarToroidalDeform>? OnGizmosCallback;

        private AvatarObjectReference _target = new();

        public AvatarObjectReference Target
        {
            get => _target;
            set => _target = value;
        }

        public float radius;
        public float aspectRatio;
        public float falloffStartAngle = 30;
        public float falloffEndAngle = 45;
        public bool disableBackHalf;

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