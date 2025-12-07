#nullable enable
using nadena.dev.modular_avatar.ui;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [FilePath("ProjectSettings/Packages/nadena.dev.modular_avatar/CheckOutfitDropPrefs.json", FilePathAttribute.Location.ProjectFolder)]
    public class CheckOutfitDropPrefs : ScriptableSingleton<CheckOutfitDropPrefs>
    {
        [SerializeField] private bool enableCheckOutfitDrop = false;
        public static bool EnableCheckOutfitDrop
        {
            get => instance.enableCheckOutfitDrop;
            set
            {
                if (instance.enableCheckOutfitDrop == value) return;
                instance.enableCheckOutfitDrop = value;
                instance.Save(true);
            }
        }

        [MenuItem(UnityMenuItems.TopMenu_CheckOutfitDrop, true)]
        static bool ValidateCheckOutfitDrop()
        {
            Menu.SetChecked(UnityMenuItems.TopMenu_CheckOutfitDrop, EnableCheckOutfitDrop);
            return true;
        }

        [MenuItem(UnityMenuItems.TopMenu_CheckOutfitDrop, false, UnityMenuItems.TopMenu_CheckOutfitDropOrder)]
        static void ToggleCheckOutfitDrop()
        {
            EnableCheckOutfitDrop = !EnableCheckOutfitDrop;
        }
    }
}