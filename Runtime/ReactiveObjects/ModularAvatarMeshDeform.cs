#nullable enable

using System;

namespace nadena.dev.modular_avatar.core
{
    public class ModularAvatarMeshDeform : AvatarTagComponent
    {
        internal static Action<ModularAvatarMeshDeform>? OnGizmosCallback;
        
        public AvatarObjectReference Target = new();
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