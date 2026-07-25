using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.editor.version
{
    internal static class VersionRecorder
    {
        private enum VersionTagUpdateMode
        {
            Automatic,
            SetCurrent,
            ClearMinimumVersion
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditWatcher.Instance.Changed += OnObjectChanged;
            AvatarTagComponent.OnResetAction += OnObjectChanged;
        }

        private static bool UpdateVersionTag(ref VersionTag tag, VersionTagUpdateMode mode)
        {
            if (mode == VersionTagUpdateMode.ClearMinimumVersion)
            {
                tag.MinimumVersion = null;
                return true;
            }

            if (mode == VersionTagUpdateMode.SetCurrent ||
                SemVerComparator.Instance.Compare(tag.MinimumVersion, VersionTag.Current.MinimumVersion) < 0)
            {
                tag = VersionTag.Current;
                return true;
            }

            return false;
        }

        private static void OnObjectChanged(Object obj)
        {
            UpdateVersionInformation(obj, VersionTagUpdateMode.Automatic);
        }

        internal static void ForceUpdateVersionInformation(GameObject root)
        {
            UpdateVersionRecursive(root, VersionTagUpdateMode.SetCurrent);
        }

        internal static void ClearMinimumVersionInformation(GameObject root)
        {
            UpdateVersionRecursive(root, VersionTagUpdateMode.ClearMinimumVersion);
        }

        private static void UpdateVersionRecursive(GameObject root, VersionTagUpdateMode mode)
        {
            foreach (var component in root.GetComponentsInChildren<AvatarTagComponent>(true))
            {
                Undo.RecordObject(component, "Update Modular Avatar Version Tag");

                UpdateVersionInformation(component, mode);
            }
        }

        private static bool UpdateVersionInformation(Object obj, VersionTagUpdateMode mode)
        {
            if (obj is not AvatarTagComponent atc) return false;

            if (UpdateVersionTag(ref atc._modularAvatarVersionTag, mode))
            {
                EditorUtility.SetDirty(atc);
                PrefabUtility.RecordPrefabInstancePropertyModifications(atc);
                return true;
            }

            return false;
        }
    }
}