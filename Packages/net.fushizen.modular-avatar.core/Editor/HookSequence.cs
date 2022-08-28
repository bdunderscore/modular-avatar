namespace net.fushizen.modular_avatar.core.editor
{
    internal static class HookSequence
    {
        public const int SEQ_RESETTERS = -90000;
        public const int SEQ_MERGE_ARMATURE = SEQ_RESETTERS + 1;
        public const int SEQ_RETARGET_MESH = SEQ_MERGE_ARMATURE + 1;
        public const int SEQ_BONE_PROXY = SEQ_RETARGET_MESH + 1;
        public const int SEQ_MERGE_ANIMATORS = SEQ_BONE_PROXY + 1;
    }
}