#if !UNITY_2021_1_OR_NEWER

using Unity.Jobs;
using UnityEngine.Jobs;

namespace nadena.dev.modular_avatar.core.armature_lock
{
    internal static class Unity2019Compat
    {
        internal static JobHandle ScheduleReadOnly<T>(this T task, TransformAccessArray transforms, int batchCount,
            JobHandle dependsOn = default)
            where T : struct, IJobParallelForTransform
        {
            return task.Schedule(transforms, dependsOn);
        }
    }
}


#endif