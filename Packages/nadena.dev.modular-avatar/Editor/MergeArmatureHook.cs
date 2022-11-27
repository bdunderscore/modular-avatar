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

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MergeArmatureHook
    {
        private Dictionary<Transform, Transform> BoneRemappings = new Dictionary<Transform, Transform>();
        private HashSet<GameObject> ToDelete = new HashSet<GameObject>();
        private HashSet<IConstraint> AddedConstraints = new HashSet<IConstraint>();

        internal bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            BoneRemappings.Clear();
            ToDelete.Clear();
            AddedConstraints.Clear();

            var mergeArmatures = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeArmature>(true);

            BoneRemappings.Clear();
            ToDelete.Clear();

            foreach (var mergeArmature in mergeArmatures)
            {
                MergeArmature(mergeArmature);
                UnityEngine.Object.DestroyImmediate(mergeArmature);
            }

            foreach (var renderer in avatarGameObject.transform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = renderer.bones;
                for (int i = 0; i < bones.Length; i++) bones[i] = MapBoneReference(bones[i], Retargetable.Ignore);
                renderer.bones = bones;
                renderer.rootBone = MapBoneReference(renderer.rootBone, Retargetable.Ignore);
                renderer.probeAnchor = MapBoneReference(renderer.probeAnchor);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                UpdateBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBoneCollider>(true))
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                UpdateBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<ContactBase>(true))
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                UpdateBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<IConstraint>(true))
            {
                if (!AddedConstraints.Contains(c))
                {
                    FixupConstraint(c);
                }
            }

            foreach (var bone in ToDelete) UnityEngine.Object.DestroyImmediate(bone);

            return true;
        }

        private void FixupConstraint(IConstraint constraint)
        {
            int nSources = constraint.sourceCount;
            for (int i = 0; i < nSources; i++)
            {
                var source = constraint.GetSource(i);
                if (source.sourceTransform == null) continue;
                if (!BoneRemappings.TryGetValue(source.sourceTransform, out var remap)) continue;
                var retarget = BoneDatabase.GetRetargetedBone(remap);

                if (retarget != null)
                {
                    source.sourceTransform = retarget;
                }
                else
                {
                    source.sourceTransform = remap;
                }

                constraint.SetSource(i, source);
            }
        }

        private void UpdateBoneReferences(Component c, Retargetable retargetable = Retargetable.Disable)
        {
            SerializedObject so = new SerializedObject(c);
            SerializedProperty iter = so.GetIterator();

            bool enterChildren = true;
            while (iter.Next(enterChildren))
            {
                enterChildren = true;
                switch (iter.propertyType)
                {
                    case SerializedPropertyType.String:
                        enterChildren = false;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        if (iter.name == "m_GameObject") break;

                        if (iter.objectReferenceValue is Transform t)
                        {
                            var mapped = MapBoneReference(t, retargetable);

                            iter.objectReferenceValue = mapped;
                            ClearToDeleteFlag(mapped);
                        }
                        else if (iter.objectReferenceValue is GameObject go)
                        {
                            var mapped = MapBoneReference(go.transform, retargetable);

                            iter.objectReferenceValue = mapped?.gameObject;
                            ClearToDeleteFlag(mapped);
                        }

                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ClearToDeleteFlag(Transform t)
        {
            while (t != null && ToDelete.Contains(t.gameObject))
            {
                ToDelete.Remove(t.gameObject);
                t = t.parent;
            }
        }

        enum Retargetable
        {
            Disable,
            Ignore,
            Use
        }

        private Transform MapBoneReference(Transform bone, Retargetable retargetable = Retargetable.Disable)
        {
            if (bone != null && BoneRemappings.TryGetValue(bone, out var newBone))
            {
                if (retargetable == Retargetable.Disable) BoneDatabase.MarkNonRetargetable(newBone);
                bone = newBone;
            }

            if (bone != null && retargetable == Retargetable.Use)
            {
                var retargeted = BoneDatabase.GetRetargetedBone(bone);
                if (retargeted != null) bone = retargeted;
            }

            return bone;
        }

        private bool HasAdditionalComponents(GameObject go, out Type constraintType)
        {
            bool hasComponents = false;
            bool needsConstraint = false;
            bool hasPositionConstraint = false;
            bool hasRotationConstraint = false;

            foreach (Component c in go.GetComponents<Component>())
            {
                switch (c)
                {
                    case Transform _: break;
                    case ModularAvatarMergeArmature _: break;
                    case VRCPhysBone _:
                    case VRCPhysBoneCollider _:
                        hasComponents = true;
                        break;
                    case AimConstraint _:
                    case LookAtConstraint _:
                    case RotationConstraint _:
                        hasRotationConstraint = true;
                        needsConstraint = true;
                        hasComponents = true;
                        break;
                    case PositionConstraint _:
                        hasPositionConstraint = true;
                        needsConstraint = true;
                        hasComponents = true;
                        break;
                    case ParentConstraint _:
                        needsConstraint = false;
                        hasPositionConstraint = hasRotationConstraint = true;
                        hasComponents = true;
                        break;
                    default:
                        hasComponents = true;
                        needsConstraint = true;
                        break;
                }
            }

            if (!needsConstraint || (hasPositionConstraint && hasRotationConstraint))
            {
                constraintType = null;
            }
            else if (hasPositionConstraint)
            {
                constraintType = typeof(RotationConstraint);
            }
            else if (hasRotationConstraint)
            {
                constraintType = typeof(PositionConstraint);
            }
            else
            {
                constraintType = typeof(ParentConstraint);
            }

            return hasComponents;
        }

        private void MergeArmature(ModularAvatarMergeArmature mergeArmature)
        {
            // TODO: error reporting framework?
            if (mergeArmature.mergeTargetObject == null) return;

            RecursiveMerge(mergeArmature, mergeArmature.gameObject, mergeArmature.mergeTargetObject.gameObject, true);
        }

        /**
         * (Attempts to) merge the source gameobject into the target gameobject. Returns true if the merged source
         * object must be retained.
         */
        private bool RecursiveMerge(
            ModularAvatarMergeArmature config,
            GameObject src,
            GameObject newParent,
            bool zipMerge
        )
        {
            GameObject mergedSrcBone = new GameObject(src.name + "@" + GUID.Generate());
            mergedSrcBone.transform.SetParent(src.transform.parent);
            mergedSrcBone.transform.localPosition = src.transform.localPosition;
            mergedSrcBone.transform.localRotation = src.transform.localRotation;
            mergedSrcBone.transform.localScale = src.transform.localScale;
            mergedSrcBone.transform.SetParent(newParent.transform, true);

            bool retain = HasAdditionalComponents(src, out var constraintType);
            if (constraintType != null)
            {
                IConstraint constraint = (IConstraint) src.AddComponent(constraintType);
                AddedConstraints.Add(constraint);
                constraint.AddSource(new ConstraintSource()
                {
                    weight = 1,
                    sourceTransform = mergedSrcBone.transform
                });
                Matrix4x4 targetToSrc = src.transform.worldToLocalMatrix * newParent.transform.localToWorldMatrix;
                if (constraint is ParentConstraint pc)
                {
                    pc.translationOffsets = new Vector3[] {targetToSrc.MultiplyPoint(Vector3.zero)};
                    pc.rotationOffsets = new Vector3[] {targetToSrc.rotation.eulerAngles};
                }

                constraint.locked = true;
                constraint.constraintActive = true;
            }

            if ((constraintType != null && constraintType != typeof(ParentConstraint))
                || (constraintType == null && src.GetComponent<IConstraint>() != null))
            {
                return true;
            }

            if (zipMerge)
            {
                BoneDatabase.AddMergedBone(mergedSrcBone.transform);
                var srcPath = RuntimeUtil.AvatarRootPath(src);
                PathMappings.Remap(srcPath, new PathMappings.MappingEntry()
                {
                    transformPath = RuntimeUtil.AvatarRootPath(newParent),
                    path = srcPath
                });
            }

            BoneRemappings[src.transform] = mergedSrcBone.transform;

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