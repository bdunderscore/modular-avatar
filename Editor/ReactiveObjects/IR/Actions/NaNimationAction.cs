#nullable enable

#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class NaNimationAction : IAction
    {
        public NaNimationAction(TargetProp targetProp, List<UnityEngine.GameObject> bones, bool shouldDelete)
        {
            TargetProp = targetProp;
            Bones = bones;
            ShouldDelete = shouldDelete;
        }

        internal TargetProp TargetProp { get; }
        internal List<UnityEngine.GameObject> Bones { get; }
        internal bool ShouldDelete { get; }
        public object TargetKey => TargetProp;
    }
}

#endif
