using NUnit.Framework;
using UnityEditor;

namespace _ModularAvatar.EditModeTests
{
    /// <summary>
    /// This tests compilation error in runtime build with building asset bundle.
    /// </summary>
    public class BuildAssetBundle
    {
        [Test]
        public void Build()
        {
            var path = "Packages/nadena.dev.modular-avatar/Assets/FixedPrefab.prefab";

            var assetBundleBuild = new AssetBundleBuild()
            {
                assetNames = new string[]{ path },
                assetBundleName = "asset.unity3d"
            };

            BuildPipeline.BuildAssetBundles("Assets/",
                new [] {assetBundleBuild},
                BuildAssetBundleOptions.None, 
                EditorUserBuildSettings.activeBuildTarget);
        }
    }
}