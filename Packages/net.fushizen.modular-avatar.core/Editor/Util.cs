using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    internal class CleanupTempAssets : IVRCSDKPostprocessAvatarCallback
    {
        public int callbackOrder => 99999;
        public void OnPostprocessAvatar()
        {
            Util.DeleteTemporaryAssets();
        }
    }
    
    public static class Util
    {
        internal const string generatedAssetsSubdirectory = "999_Modular_Avatar_Generated";
        internal const string generatedAssetsPath = "Assets/" + generatedAssetsSubdirectory;

        static internal AnimatorController CreateContainer()
        {
            var container = new AnimatorController();
            AssetDatabase.CreateAsset(container, GenerateAssetPath());

            return container;
        }

        internal static string GenerateAssetPath()
        {
            return GetGeneratedAssetsFolder() + "/" + GUID.Generate() + ".asset";
        }

        internal static string GetGeneratedAssetsFolder()
        {
            if (!AssetDatabase.IsValidFolder(generatedAssetsPath))
            {
                AssetDatabase.CreateFolder("Assets", generatedAssetsSubdirectory);
            }

            return generatedAssetsPath;
        }

        static internal void DeleteTemporaryAssets()
        {
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.SaveAssets();
                
                var subdir = generatedAssetsPath;

                AssetDatabase.DeleteAsset(subdir);
                //FileUtil.DeleteFileOrDirectory(subdir);
            };
        }
    }
}