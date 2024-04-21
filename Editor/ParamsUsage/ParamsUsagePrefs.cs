using System;
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine.Serialization;

namespace nadena.dev.modular_avatar.core.editor
{
    [FilePath("modular-avatar/ParamsUsagePrefs.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal sealed class ParamsUsagePrefs : ScriptableSingleton<ParamsUsagePrefs>
    {
        public static event Action<bool> OnChange_EnableInfoMenu;
        
        [FormerlySerializedAs("EnableInfoMenu")] public bool enableInfoMenu = true;
        
        [MenuItem(UnityMenuItems.TopMenu_EnableInfo, false, UnityMenuItems.TopMenu_EnableInfoOrder)]
        private static void Menu_EnableInfo()
        {
            ParamsUsagePrefs.instance.enableInfoMenu = !ParamsUsagePrefs.instance.enableInfoMenu;
            Menu.SetChecked(UnityMenuItems.TopMenu_EnableInfo, ParamsUsagePrefs.instance.enableInfoMenu);
            ParamsUsagePrefs.instance.Save(true);
            
            OnChange_EnableInfoMenu?.Invoke(ParamsUsagePrefs.instance.enableInfoMenu);
        }
        
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Menu.SetChecked(UnityMenuItems.TopMenu_EnableInfo, ParamsUsagePrefs.instance.enableInfoMenu);
        }
    }
}