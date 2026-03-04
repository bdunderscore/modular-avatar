#nullable enable

#region

using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPass : Pass<ScaleAdjusterPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            HashSet<Transform> adjustedBones = new(); 
            Dictionary<Transform, Transform> boneMappings = new Dictionary<Transform, Transform>();

            foreach (var adjuster in context.AvatarRootObject.GetComponentsInChildren<ModularAvatarScaleAdjuster>(true))
            {
                adjustedBones.Add(adjuster.transform);
                
                var proxyObject = new GameObject("ScaleProxy");
                var proxyTransform = proxyObject.transform;
                proxyObject.AddComponent<ModularAvatarPBBlocker>();

                proxyTransform.SetParent(adjuster.transform, false);
                proxyTransform.localPosition = Vector3.zero;
                proxyTransform.localRotation = Quaternion.identity;
                proxyTransform.localScale = adjuster.Scale;

                boneMappings.Add(adjuster.transform, proxyTransform);

                Object.DestroyImmediate(adjuster);
            }
            
            if (boneMappings.Count == 0)
            {
                return;
            }

            foreach (var smr in context.AvatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && boneMappings.TryGetValue(bones[i], out var newBone))
                    {
                        bones[i] = newBone;
                    }
                }
                smr.bones = bones;
            }
            
            // Correct the humanoid avatar descriptor for any bones we might have moved (that is - any children of
            // Scale Adjusters).

            HumanoidAvatarDescriptorRebuilder.Rebuild(context, (hbm, humanDesc) =>
            {
                if (hbm.Bone == null || !adjustedBones.Contains(hbm.Bone)) return;

                foreach (var child in hbm.Children)
                {
                    if (child.Bone == null) continue;
                    humanDesc.skeleton[child.BoneIndex].position = child.Bone.localPosition;
                }
            });
        }
    }
}
