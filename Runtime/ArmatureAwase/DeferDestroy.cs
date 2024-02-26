#region

using System;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal static class DeferDestroy
    {
        private static bool _immediate = false;

        internal static void DestroyImmediate(IDisposable obj)
        {
            var oldValue = _immediate;
            _immediate = true;
            try
            {
                obj.Dispose();
            }
            finally
            {
                _immediate = oldValue;
            }
        }

        internal static void DeferDestroyObj(IDisposable obj)
        {
            if (_immediate)
            {
                obj.Dispose();
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => obj.Dispose();
#endif
        }
    }
}