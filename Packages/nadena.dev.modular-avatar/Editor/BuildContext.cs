using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BuildContext
    {
        internal readonly VRCAvatarDescriptor AvatarDescriptor;
        internal readonly AnimationDatabase AnimationDatabase = new AnimationDatabase();

        public BuildContext(VRCAvatarDescriptor avatarDescriptor)
        {
            AvatarDescriptor = avatarDescriptor;
        }
    }
}