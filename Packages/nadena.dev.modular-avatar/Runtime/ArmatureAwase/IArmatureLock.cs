using System;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal interface IArmatureLock : IDisposable
    {
        LockResult Execute();
        bool IsStable();
    }
}