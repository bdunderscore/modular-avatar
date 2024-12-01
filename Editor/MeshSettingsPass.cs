﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class InheritModeExtension
    {
        internal static bool NotFinal(this ModularAvatarMeshSettings.InheritMode mode)
        {
            return mode is ModularAvatarMeshSettings.InheritMode.Inherit or ModularAvatarMeshSettings.InheritMode.SetOrInherit;
        }
    }

    internal class MeshSettingsPass
    {
        private readonly BuildContext context;

        public MeshSettingsPass(BuildContext context)
        {
            this.context = context;
        }

        public void OnPreprocessAvatar()
        {
            foreach (var mesh in context.AvatarRootObject.GetComponentsInChildren<Renderer>(true))
            {
                ProcessMesh(mesh);
            }
        }

        internal struct MergedSettings
        {
            public bool SetAnchor, SetBounds;

            public Transform ProbeAnchor;
            public Transform RootBone;
            public Bounds Bounds;
        }

        // current Mode is the mode of current value, and the current value is came from MA Mesh Settings of child GameObject
        // the srcMode is the mode of currently processing MA Mesh Settings, which is the parent component of the current value
        private static bool ShouldUseSrcValue(
            ref ModularAvatarMeshSettings.InheritMode currentMode,
            ModularAvatarMeshSettings.InheritMode srcMode)
        {
            switch (currentMode, srcMode)
            {
                // invalid cases
                case (not (ModularAvatarMeshSettings.InheritMode.Set
                    or ModularAvatarMeshSettings.InheritMode.Inherit
                    or ModularAvatarMeshSettings.InheritMode.DontSet
                    or ModularAvatarMeshSettings.InheritMode.SetOrInherit), _):
                    throw new InvalidOperationException($"Logic failure: invalid InheritMode: {currentMode}");
                case (_, not (ModularAvatarMeshSettings.InheritMode.Set
                    or ModularAvatarMeshSettings.InheritMode.Inherit
                    or ModularAvatarMeshSettings.InheritMode.DontSet
                    or ModularAvatarMeshSettings.InheritMode.SetOrInherit)):
                    throw new ArgumentOutOfRangeException(nameof(srcMode), $"Invalid InheritMode: {srcMode}");

                // If current value is came from Set or DontSet, it should not be changed
                case (ModularAvatarMeshSettings.InheritMode.Set, _):
                case (ModularAvatarMeshSettings.InheritMode.DontSet, _):
                    return false;
                // If srcMode is Inherit, it should not be changed
                case (_, ModularAvatarMeshSettings.InheritMode.Inherit):
                    return false;

                // If srcMode is DontSet, the value will not be used but mode should be used
                case (_, ModularAvatarMeshSettings.InheritMode.DontSet):
                    currentMode = srcMode;
                    return true;

                // if SrcMode is Set or SetOrInherit, it should be used.
                case (_, ModularAvatarMeshSettings.InheritMode.Set):
                case (_, ModularAvatarMeshSettings.InheritMode.SetOrInherit):
                    currentMode = srcMode;
                    return true;
            }
        }

        internal static MergedSettings MergeSettings(Transform avatarRoot, Transform referenceObject)
        {
            MergedSettings merged = new MergedSettings();

            Transform current = referenceObject;

            ModularAvatarMeshSettings.InheritMode inheritProbeAnchor = ModularAvatarMeshSettings.InheritMode.Inherit;
            ModularAvatarMeshSettings.InheritMode inheritBounds = ModularAvatarMeshSettings.InheritMode.Inherit;

            do
            {
                var settings = current.GetComponent<ModularAvatarMeshSettings>();
                if (current == avatarRoot)
                {
                    current = null;
                }
                else
                {
                    current = current.transform.parent;
                }

                if (settings == null)
                {
                    continue;
                }

                if (ShouldUseSrcValue(ref inheritProbeAnchor, settings.InheritProbeAnchor))
                {
                    merged.ProbeAnchor = settings.ProbeAnchor.Get(settings)?.transform;
                }

                if (ShouldUseSrcValue(ref inheritBounds, settings.InheritBounds))
                {
                    merged.RootBone = settings.RootBone.Get(settings)?.transform;
                    merged.Bounds = settings.Bounds;
                }
            } while (current != null && (inheritProbeAnchor.NotFinal() || inheritBounds.NotFinal()));

            merged.SetAnchor = inheritProbeAnchor is ModularAvatarMeshSettings.InheritMode.Set or ModularAvatarMeshSettings.InheritMode.SetOrInherit;
            merged.SetBounds = inheritBounds is ModularAvatarMeshSettings.InheritMode.Set or ModularAvatarMeshSettings.InheritMode.SetOrInherit;

            return merged;
        }

        private void ProcessMesh(Renderer mesh)
        {
            MergedSettings settings = MergeSettings(context.AvatarRootTransform, mesh.transform);

            if (settings.SetAnchor)
            {
                mesh.probeAnchor = settings.ProbeAnchor;
            }

            if (settings.SetBounds && mesh is SkinnedMeshRenderer smr)
            {
                if (smr.bones.Length == 0 && smr.sharedMesh)
                {
                    Mesh newMesh = Object.Instantiate(smr.sharedMesh);
                    smr.sharedMesh = newMesh;
                    smr.bones = new Transform[] { smr.transform };
                    smr.rootBone = smr.transform;
                    smr.sharedMesh.boneWeights = Enumerable.Repeat(new BoneWeight() { boneIndex0 = 0, weight0 = 1 }, newMesh.vertexCount).ToArray();
                    smr.sharedMesh.bindposes = new Matrix4x4[] { smr.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix };

                    if (newMesh) context.SaveAsset(newMesh);
                }

                var settingsRootBone = settings.RootBone;
                settingsRootBone = settingsRootBone == null ? smr.transform : settingsRootBone;
                var smrRootBone = smr.rootBone;
                smrRootBone = smrRootBone == null ? smr.transform : smrRootBone;

                if (IsInverted(smrRootBone) != IsInverted(settingsRootBone))
                {
                    smr.rootBone = GetInvertedRootBone(settingsRootBone);

                    var bounds = settings.Bounds;
                    var center = bounds.center;
                    center.x *= -1;
                    bounds.center = center;
                    smr.localBounds = bounds;
                }
                else
                {
                    smr.rootBone = settings.RootBone;
                    smr.localBounds = settings.Bounds;
                }
            }
        }

        private bool IsInverted(Transform bone)
        {
            var inverseCount = 0;

            var scale = bone.lossyScale;
            if (scale.x < 0) inverseCount += 1;
            if (scale.y < 0) inverseCount += 1;
            if (scale.z < 0) inverseCount += 1;

            return (inverseCount % 2) != 0;
        }
        private Dictionary<Transform, Transform> invertedRootBoneCache = new();
        private Transform GetInvertedRootBone(Transform rootBone)
        {
            if (invertedRootBoneCache.TryGetValue(rootBone, out var cache)) { return cache; }

            var invertedRootBone = new GameObject($"{rootBone.gameObject.name}-InvertedRootBone");
            EditorUtility.CopySerialized(rootBone, invertedRootBone.transform);
            invertedRootBone.transform.parent = rootBone;

            var transform = invertedRootBone.transform;
            var scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;

            invertedRootBoneCache[rootBone] = transform;
            return transform;
        }
    }
}
