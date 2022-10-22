using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.PackageManagement.PackageMaker;

public class PackageMakerWindowData : ScriptableObject
{
    public static string defaultAssetPath = Path.Combine("Assets", "PackageMakerWindowData.asset");
    public string targetAssetFolder;
    public string packageID;
    public PackageMakerWindow.VRCPackageEnum relatedPackage;

    public static PackageMakerWindowData GetOrCreate()
    {
        var existingData = AssetDatabase.AssetPathToGUID(defaultAssetPath);
        if (string.IsNullOrWhiteSpace(existingData))
        {
            return Create();
        }
        else
        {
            var saveData = AssetDatabase.LoadAssetAtPath<PackageMakerWindowData>(defaultAssetPath);
            if (saveData == null)
            {
                Debug.LogError($"Could not load saved data but the save file exists. Resetting.");
                return Create();
            }
            return saveData;
        }
    }

    public static PackageMakerWindowData Create()
    {
        var saveData = CreateInstance<PackageMakerWindowData>();
        AssetDatabase.CreateAsset(saveData, defaultAssetPath);
        AssetDatabase.SaveAssets();
        return saveData;
    }

    public void Save()
    {
        AssetDatabase.SaveAssets();
    }
}
