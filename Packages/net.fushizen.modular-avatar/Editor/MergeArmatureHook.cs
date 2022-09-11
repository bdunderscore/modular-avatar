/*
 * MIT License
 * 
 * Copyright (c) 2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

namespace net.fushizen.modular_avatar.core.editor
{
    public class MergeArmatureHook
    {
        private Dictionary<Transform, Transform> BoneRemappings = new Dictionary<Transform, Transform>();
        private List<GameObject> ToDelete = new List<GameObject>();

        internal bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            BoneRemappings.Clear();
            ToDelete.Clear();
            
            var mergeArmatures = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeArmature>(true);
            
            BoneRemappings.Clear();
            ToDelete.Clear();
            
            foreach (var mergeArmature in mergeArmatures)
            {
                MergeArmature(mergeArmature);
                UnityEngine.Object.DestroyImmediate(mergeArmature);
            }

            foreach (var renderer in avatarGameObject.transform.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bones = renderer.bones;
                for (int i = 0; i < bones.Length; i++) bones[i] = MapBoneReference(bones[i], false);
                renderer.bones = bones;
                renderer.rootBone = MapBoneReference(renderer.rootBone);
                renderer.probeAnchor = MapBoneReference(renderer.probeAnchor);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBone>())
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                UpdateBoneReferences(c);
            }
            
            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBoneCollider>())
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                UpdateBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<ContactBase>())
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                UpdateBoneReferences(c);
            }
            
            foreach (var bone in ToDelete) UnityEngine.Object.DestroyImmediate(bone);

            return true;
        }

        private void UpdateBoneReferences(Component c)
        {
            SerializedObject so = new SerializedObject(c);
            SerializedProperty iter = so.GetIterator();

            bool enterChildren = true;
            while (iter.Next(enterChildren))
            {
                enterChildren = true;
                switch (iter.propertyType)
                {
                    case SerializedPropertyType.String: enterChildren = false;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        if (iter.objectReferenceValue is Transform t)
                        {
                            iter.objectReferenceValue = MapBoneReference(t);
                        }

                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private Transform MapBoneReference(Transform bone, bool markNonRetargetable = true)
        {
            if (bone != null && BoneRemappings.TryGetValue(bone, out var newBone))
            {
                if (markNonRetargetable) BoneDatabase.MarkNonRetargetable(newBone);
                bone = newBone;
            }
            return bone;
        }

        private bool HasAdditionalComponents(GameObject go, out bool needsConstraint)
        {
            bool hasComponents = false;
            needsConstraint = false;
            
            foreach (Component c in go.GetComponents<Component>())
            {
                switch (c)
                {
                    case Transform _: break;
                    case ModularAvatarMergeArmature _: break;
                    case MAInternalOffsetMarker _: break;
                    case VRCPhysBone _: case VRCPhysBoneCollider _: hasComponents = true;
                        break;
                    default:
                        hasComponents = true;
                        needsConstraint = true;
                        break;
                }
            }

            return hasComponents;
        }
        
        private void MergeArmature(ModularAvatarMergeArmature mergeArmature)
        {
            // TODO: error reporting framework?
            if (mergeArmature.mergeTarget == null) return;

            RecursiveMerge(mergeArmature, mergeArmature.gameObject, mergeArmature.mergeTarget.gameObject, true);
        }

        /**
         * (Attempts to) merge the source gameobject into the target gameobject. Returns true if the merged source
         * object must be retained.
         */
        private bool RecursiveMerge(ModularAvatarMergeArmature config, GameObject src, GameObject newParent, bool zipMerge)
        {
            GameObject mergedSrcBone = new GameObject(src.name + "@" + GUID.Generate());
            mergedSrcBone.transform.SetParent(src.transform.parent);
            mergedSrcBone.transform.localPosition = src.transform.localPosition;
            mergedSrcBone.transform.localRotation = src.transform.localRotation;
            mergedSrcBone.transform.localScale = src.transform.localScale;

            if (zipMerge)
            {
                BoneDatabase.AddMergedBone(mergedSrcBone.transform);
                var srcPath = RuntimeUtil.AvatarRootPath(src);
                PathMappings.Remap(srcPath, new PathMappings.MappingEntry()
                {
                    transformPath = zipMerge ? RuntimeUtil.AvatarRootPath(newParent) : srcPath,
                    path = srcPath
                });
            }
            mergedSrcBone.transform.SetParent(newParent.transform, true);
            BoneRemappings[src.transform] = mergedSrcBone.transform;

            bool retain = HasAdditionalComponents(src, out bool needsConstraint);
            if (needsConstraint)
            {
                ParentConstraint constraint = src.AddComponent<ParentConstraint>();
                constraint.AddSource(new ConstraintSource()
                {
                    weight = 1,
                    sourceTransform = mergedSrcBone.transform
                });
                Matrix4x4 targetToSrc = src.transform.worldToLocalMatrix * newParent.transform.localToWorldMatrix;  
                constraint.translationOffsets = new Vector3[] {targetToSrc.MultiplyPoint(Vector3.zero)};
                constraint.rotationOffsets = new Vector3[] {targetToSrc.rotation.eulerAngles};
                constraint.locked = true;
                constraint.constraintActive = true;
            }
            
            List<Transform> children = new List<Transform>();
            foreach (Transform child in src.transform)
            {
                children.Add(child);
            }
            foreach (Transform child in children)
            {
                var childGameObject = child.gameObject;
                var childName = childGameObject.name;
                GameObject childNewParent = mergedSrcBone;
                bool shouldZip = zipMerge;

                if (shouldZip && childName.StartsWith(config.prefix) && childName.EndsWith(config.suffix))
                {
                    var targetObjectName = childName.Substring(config.prefix.Length,
                        childName.Length - config.prefix.Length - config.suffix.Length);
                    var targetObject = newParent.transform.Find(targetObjectName);
                    if (targetObject != null)
                    {
                        childNewParent = targetObject.gameObject;
                    }
                    else
                    {
                        shouldZip = false;
                    }
                }
                
                

                var retainChild = RecursiveMerge(config, childGameObject, childNewParent, shouldZip);
                retain = retain || retainChild;
            }

            if (!retain) ToDelete.Add(src);

            return retain;
        }
    }
}