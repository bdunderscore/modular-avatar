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
            HierarchyPrefabDropHook.OnPrefabInstanceDropped += OnPrefabDropped;
        }

        private static void OnPrefabDropped(List<GameObject> droppedObjects)
        {
            var targets = new List<GameObject>();
            foreach (var droppedObject in droppedObjects)
            {
                if (!SetupOutfit.ValidateSetupOutfit(droppedObject, out var avatarRoot, out var avatarHips, out var outfitHips))
                {
                    continue;
                }

                if (IsAlreadyProcessed(outfitHips))
                {
                    SceneView.lastActiveSceneView.ShowNotification(new GUIContent(Localization.S("check_outfit_drop.preset_cloth")), 2.0f);
                    continue;
                }

                if (_processedTargets.Contains(avatarHips))
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
        private static bool IsAlreadyProcessed(GameObject outfitHips)
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