#region

using System;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal static class DeferDestroy
    {
        internal static void DeferDestroyObj(IDisposable obj)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => obj.Dispose();
#endif
        }
    }
}