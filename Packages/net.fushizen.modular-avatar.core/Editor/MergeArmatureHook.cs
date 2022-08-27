using System.Collections.Generic;
using Codice.CM.Common.Merge;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    public class MergeArmatureHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -3000;
        
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var mergeArmatures = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeArmature>();
            
            foreach (var mergeArmature in mergeArmatures)
            {
                MergeArmature(mergeArmature);
                UnityEngine.Object.DestroyImmediate(mergeArmature);
            }

            return true;
        }

        private void MergeArmature(ModularAvatarMergeArmature mergeArmature)
        {
            // TODO: error reporting framework?
            if (mergeArmature.mergeTarget == null) return;

            RecursiveMerge(mergeArmature, mergeArmature.gameObject, mergeArmature.mergeTarget.gameObject);
        }

        private void RecursiveMerge(ModularAvatarMergeArmature config, GameObject src, GameObject target)
        {
            src.transform.SetParent(target.transform, true);
            List<Transform> children = new List<Transform>();
            foreach (Transform child in src.transform)
            {
                children.Add(child);
            }
            foreach (Transform child in children)
            {
                var childGameObject = child.gameObject;
                var childName = childGameObject.name;
                if (childName.StartsWith(config.prefix) && childName.EndsWith(config.suffix))
                {
                    var targetObjectName = childName.Substring(config.prefix.Length, 
                        childName.Length - config.prefix.Length - config.suffix.Length);
                    var targetObject = target.transform.Find(targetObjectName);
                    if (targetObject != null)
                    {
                        RecursiveMerge(config, childGameObject, targetObject.gameObject);
                    }
                }
            }
        }
    }
}