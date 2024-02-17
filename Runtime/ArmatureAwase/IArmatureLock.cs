using System;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal interface IArmatureLock : IDisposable
    {
        void Prepare();
        LockResult Execute();
        bool IsStable();
    }
}