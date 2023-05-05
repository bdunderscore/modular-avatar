namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// Tag class which overrides the default configuration of a control group. This is primarily used for asset-based
    /// shared control groups which can't be configured in place.
    /// </summary>
    public class SetGroupDefaults : AvatarTagComponent
    {
        public bool isSynced;
        public bool isSaved;
        public ModularAvatarMenuItem defaultValue;
        public ControlGroup targetControlGroup;
    }
}