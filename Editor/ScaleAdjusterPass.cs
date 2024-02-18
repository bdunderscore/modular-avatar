using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor.EditorTools;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPass : Pass<ScaleAdjusterPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            ScaleAdjusterRenderer.ClearAllOverrides();

            Dictionary<Transform, Transform> boneMappings = new Dictionary<Transform, Transform>();
            foreach (var component in context.AvatarRootObject.GetComponentsInChildren<ScaleProxy>())
            {
                var proxyTransform = component.transform;
                var parentAdjuster = component.transform.parent?.GetComponent<ModularAvatarScaleAdjuster>();
                if (parentAdjuster != null)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                    
                    proxyTransform.localScale = parentAdjuster.Scale;
                    UnityEngine.Object.DestroyImmediate(parentAdjuster);
                
                    boneMappings.Add(proxyTransform.parent, proxyTransform);
                }
            }
                        
            foreach (var sar in context.AvatarRootObject.GetComponentsInChildren<ScaleAdjusterRenderer>())
            {
                UnityEngine.Object.DestroyImmediate(sar.gameObject);
            }
            
            foreach (var smr in context.AvatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bones = smr.bones;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (boneMappings.TryGetValue(bones[i], out var newBone))
                    {
                        bones[i] = newBone;
                    }
                }
                smr.bones = bones;
            }

        }
    }
}