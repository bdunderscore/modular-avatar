using System;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal static class UpdateLoopController
    {
        internal static event Action OnArmatureLockUpdate;
        internal static event Action OnMoveIndependentlyUpdate;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += () =>
            {
                OnArmatureLockUpdate?.Invoke();
                OnMoveIndependentlyUpdate?.Invoke();
            };
#endif
        }
#endif
    }
}