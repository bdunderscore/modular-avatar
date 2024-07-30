#region

using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPass : Pass<ScaleAdjusterPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            Dictionary<Transform, Transform> boneMappings = new Dictionary<Transform, Transform>();

            foreach (var adjuster in context.AvatarRootObject.GetComponentsInChildren<ModularAvatarScaleAdjuster>(true))
            {
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

            // Legacy cleanup
            /*foreach (var sar in context.AvatarRootObject.GetComponentsInChildren<ScaleAdjusterRenderer>())
            {
                Object.DestroyImmediate(sar.gameObject);
            }
            foreach (var sar in context.AvatarRootObject.GetComponentsInChildren<ScaleProxy>())
            {
                Object.DestroyImmediate(sar.gameObject);
            }*/
            
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

        }
    }
}