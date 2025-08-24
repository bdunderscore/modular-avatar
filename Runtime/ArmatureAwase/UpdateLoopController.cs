#region

using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEditor;

#endregion

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal static class UpdateLoopController
    {
#pragma warning disable CS0067 // Event is never used (only used in UNITY_EDITOR builds)
        internal static event Action UpdateCallbacks;
        internal static event Action OnMoveIndependentlyUpdate;
#pragma warning restore CS0067

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.update += Update;
        }

        private static List<JobHandle> jobs = new List<JobHandle>();

        private static void Update()
        {
            if (ArmatureLockConfig.instance.GlobalEnable)
            {
                UpdateCallbacks?.Invoke();
            }

            OnMoveIndependentlyUpdate?.Invoke();
        }
#endif
    }
}