#nullable enable

using nadena.dev.modular_avatar.ui;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class ReactiveDebugDump
    {
        internal const string SessionStateKey = "nadena.dev.modular-avatar.reactive-components.debug-dump";

        internal static bool Enabled
        {
            get => SessionState.GetBool(SessionStateKey, false);
            set => SessionState.SetBool(SessionStateKey, value);
        }

        [MenuItem(UnityMenuItems.TopMenu_ReactiveComponentDebugDump, false,
            UnityMenuItems.TopMenu_ReactiveComponentDebugDumpOrder)]
        private static void Toggle()
        {
            Enabled = !Enabled;
        }

        [MenuItem(UnityMenuItems.TopMenu_ReactiveComponentDebugDump, true,
            UnityMenuItems.TopMenu_ReactiveComponentDebugDumpOrder)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(UnityMenuItems.TopMenu_ReactiveComponentDebugDump, Enabled);
            return true;
        }
    }
}
