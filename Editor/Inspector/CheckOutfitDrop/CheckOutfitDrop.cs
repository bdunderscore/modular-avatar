#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class CheckOutfitDrop
    {
        private static readonly HashSet<GameObject> _processedTargets = new();

        [InitializeOnLoadMethod]
        static void Init()
        {
            HierarchyPrefabDropHook.OnPrefabAssetDropped += OnPrefabDropped;
            // Also target prefab instances in scenes. 
            // This is intended for a prefab instance that was mistakenly placed at the scene root by a previous D&D to be targeted when it is re-dropped.
            HierarchyPrefabDropHook.OnPrefabInstanceDropped += OnPrefabDropped;
        }

        private static void OnPrefabDropped(List<GameObject> droppedObjects)
        {
            if (!CheckOutfitDropPrefs.EnableCheckOutfitDrop) return;

            var targets = new List<GameObject>();
            foreach (var droppedObject in droppedObjects)
            {
                if (!SetupOutfit.ValidateSetupOutfit(droppedObject, out var avatarRoot, out var avatarHips, out var outfitHips))
                {
                    continue;
                }

                if (IsSetuppedOutfit(outfitHips))
                {
                    var seneview = SceneView.lastActiveSceneView;
                    if (seneview != null)
                    {
                        seneview.ShowNotification(new GUIContent(Localization.S("check_outfit_drop.preset_cloth")), 2.0f);
                    }
                    continue;
                }

                if (_processedTargets.Contains(droppedObject))
                {
                    continue;
                }

                targets.Add(droppedObject);
            }

            if (targets.Count > 0)
            {
                foreach (var target in targets)
                {
                    _processedTargets.Add(target);
                }
                SetupOutfitWindow.AddEntries(targets);
            }
        }

        // Do not target "Preset" or prefabs that have already executed setup outfit as much as possible.
        private static bool IsSetuppedOutfit(GameObject outfitHips)
        {
            var outfitArmature = outfitHips.transform.parent;

            var mergeArmature = outfitArmature.GetComponent<ModularAvatarMergeArmature>();
            if (mergeArmature != null && mergeArmature.mergeTargetObject != null)
            {
                return true;
            }

            return false;
        }
    }
} 