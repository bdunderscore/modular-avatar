using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace nadena.dev.modular_avatar.editor.version
{
    internal static class VersionTagInitializer
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(VersionTag).Assembly);
            if (packageInfo != null)
            {
                VersionTag.SetCurrentVersion(packageInfo.version);
            }
        }
    }
}