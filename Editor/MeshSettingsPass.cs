using System.Linq;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class InheritModeExtension
    {
        internal static bool NotFinal(this ModularAvatarMeshSettings.InheritMode mode)
        {
            return mode == ModularAvatarMeshSettings.InheritMode.Inherit;
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

        private static bool Inherit(ref ModularAvatarMeshSettings.InheritMode mode,
            ModularAvatarMeshSettings.InheritMode srcmode)
        {
            if (mode != ModularAvatarMeshSettings.InheritMode.Inherit ||
                srcmode == ModularAvatarMeshSettings.InheritMode.Inherit)
                return false;

            mode = srcmode;
            return true;
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

                if (Inherit(ref inheritProbeAnchor, settings.InheritProbeAnchor))
                {
                    merged.ProbeAnchor = settings.ProbeAnchor.Get(settings)?.transform;
                }

                if (Inherit(ref inheritBounds, settings.InheritBounds))
                {
                    merged.RootBone = settings.RootBone.Get(settings)?.transform;
                    merged.Bounds = settings.Bounds;
                }
            } while (current != null && (inheritProbeAnchor.NotFinal() || inheritBounds.NotFinal()));

            merged.SetAnchor = inheritProbeAnchor == ModularAvatarMeshSettings.InheritMode.Set;
            merged.SetBounds = inheritBounds == ModularAvatarMeshSettings.InheritMode.Set;

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
                smr.rootBone = settings.RootBone;
                smr.localBounds = settings.Bounds;
            }
        }
    }
}