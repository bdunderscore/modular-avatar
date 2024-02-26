using System;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal abstract class ArmatureLock : IDisposable
    {
        private bool _enableAssemblyReloadCallback;

        protected bool EnableAssemblyReloadCallback
        {
            get => _enableAssemblyReloadCallback;
            set
            {
                if (_enableAssemblyReloadCallback == value) return;
                _enableAssemblyReloadCallback = value;
#if UNITY_EDITOR
                if (value)
                {
                    UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnDomainUnload;
                }
                else
                {
                    UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnDomainUnload;
                }
#endif
            }
        }

        public abstract void Prepare();
        public abstract LockResult Execute();
        public abstract bool IsStable();
        public abstract void Dispose();

        private void OnDomainUnload()
        {
            // Unity 2019 does not call deferred callbacks before domain unload completes,
            // so we need to make sure to immediately destroy all our TransformAccessArrays.
            DeferDestroy.DestroyImmediate(this);
        }
    }
}