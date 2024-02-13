using System;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal static class UpdateLoopController
    {
        internal static event Action OnArmatureLockPrepare;
        internal static event Action OnArmatureLockUpdate;
        internal static event Action OnMoveIndependentlyUpdate;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
            UnityEditor.EditorApplication.update += () =>
            {
                if (ArmatureLockConfig.instance.GlobalEnable)
                {
                    OnArmatureLockPrepare?.Invoke();
                    OnArmatureLockUpdate?.Invoke();
                }

                OnMoveIndependentlyUpdate?.Invoke();
            };
        }
#endif

        internal static void InvokeArmatureLockPrepare()
        {
            OnArmatureLockPrepare?.Invoke();
        }
    }
}