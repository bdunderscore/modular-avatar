using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class MaterialFinder
    {
        public static List<Material> BuildCandidateList(QuickSwapMode mode, Material currentMaterial)
        {
            List<Material> xs;
            switch (mode)
            {
                case QuickSwapMode.SameDirectory:
                {
                    xs = SameDirectory(currentMaterial);
                    break;
                }
                case QuickSwapMode.SiblingDirectory:
                {
                    xs = SiblingDirectory(currentMaterial);
                    break;
                }
                default:
                {
                    xs = new List<Material>();
                    break;
                }
            }

            return xs;
        }

        private static List<Material> SameDirectory(Material currentMaterial)
        {
            var candidateMaterials = new List<Material>();
            if (currentMaterial == null)
            {
                return candidateMaterials;
            }

            var currentPath = AssetDatabase.GetAssetPath(currentMaterial);
            var directory = System.IO.Path.GetDirectoryName(currentPath);
            if (directory == null)
            {
                return candidateMaterials;
            }

            var assetsInDirectory = AssetDatabase.FindAssets("t:Material", new[] { directory })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .OrderBy(p => p);
            foreach (var assetPath in assetsInDirectory)
            {
                // Exclude subdirectories
                if (System.IO.Path.GetDirectoryName(assetPath) != directory)
                {
                    continue;
                }
                
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material != null)
                {
                    candidateMaterials.Add(material);
                }
            }

            return candidateMaterials;
        }

        private static List<Material> SiblingDirectory(Material currentMaterial)
        {
            var currentPath = AssetDatabase.GetAssetPath(currentMaterial);
            var currentDirectory = System.IO.Path.GetDirectoryName(currentPath);
            var parentDirectory = System.IO.Path.GetDirectoryName(currentDirectory);
            var currentFileName = System.IO.Path.GetFileName(currentPath);

            return AssetDatabase.FindAssets("t:Material", new[] { parentDirectory })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .GroupBy(path => System.IO.Path.GetDirectoryName(path))
                .Where(group => System.IO.Path.GetDirectoryName(group.Key) == parentDirectory)
                .Select(group =>
                    group.OrderBy(path => LevenshteinDistance(System.IO.Path.GetFileName(path), currentFileName))
                        .First()
                )
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(mat => mat != null)
                .ToList();
        }

        private static int LevenshteinDistance(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            
            var matrix = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i < a.Length; i++)
            {
                matrix[i, 0] = i + 1;
            }

            for (int j = 0; j < b.Length; j++)
            {
                matrix[0, j] = j + 1;
            }
            
            for (int j = 0; j < b.Length; j++)
            {
                for (int i = 0; i < a.Length; i++)
                {
                    int cost = (a[i] == b[j]) ? 0 : 1;
                    matrix[i + 1, j + 1] = Mathf.Min(
                        matrix[i, j + 1] + 1, // Deletion
                        matrix[i + 1, j] + 1, // Insertion
                        matrix[i, j] + cost // Substitution
                    );
                }
            }
            
            return matrix[a.Length, b.Length];
        }
    }
}