namespace nadena.dev.modular_avatar.ui
{
    internal static class UnityMenuItems
    {
        internal const string GameObject_SetupOutfit = "GameObject/Modular Avatar/Setup Outfit";
        internal const int GameObject_SetupOutfitOrder = -1000;
        
        internal const string GameObject_CreateToggle = "GameObject/Modular Avatar/Create Toggle";
        internal const int GameObject_CreateToggleOrder = GameObject_SetupOutfitOrder + 1;

        internal const string GameObject_ManualBake = "GameObject/Modular Avatar/Manual Bake Avatar";
        internal const int GameObject_ManualBakeOrder = GameObject_CreateToggleOrder + 1;

        // <separator>

        internal const string GameObject_EnableInfo = "GameObject/Modular Avatar/Show Modular Avatar Information";
        internal const int GameObject_EnableInfoOrder = -799;
        
        internal const string GameObject_ShowReactionDebugger = "GameObject/Modular Avatar/Show Reaction Debugger";
        internal const int GameObject_ShowReactionDebuggerOrder = GameObject_EnableInfoOrder + 1;

        internal const string GameObject_ExtractMenu = "GameObject/Modular Avatar/Extract Menu";
        internal const int GameObject_ExtractMenuOrder = GameObject_EnableInfoOrder + 100;
        
        
        
        
        internal const string TopMenu_EditModeBoneSync = "Tools/Modular Avatar/Sync Bones in Edit Mode";
        internal const int TopMenu_EditModeBoneSyncOrder = 100;
        
        internal const string TopMenu_EnableInfo = "Tools/Modular Avatar/Show Modular Avatar Information";
        internal const int TopMenu_EnableInfoOrder = 101;

        // <separator>
        
        internal const string TopMenu_ManualBakeAvatar = "Tools/Modular Avatar/Manual Bake Avatar";
        internal const int TopMenu_ManualBakeAvatarOrder = 1000;
        
        internal const string TopMenu_ReloadLocalizations = "Tools/Modular Avatar/Reload Localizations";
        internal const int TopMenu_ReloadLocalizationsOrder = 1001;

    }
}