using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Codice.CM.Common.Merge;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector3 = UnityEngine.Vector3;

namespace net.fushizen.modular_avatar.core.editor
{
    public class MergeArmatureHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => HookSequence.SEQ_MERGE_ARMATURE;

        private Dictionary<Transform, Transform> BoneRemappings = new Dictionary<Transform, Transform>();
        private List<GameObject> ToDelete = new List<GameObject>();

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
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
                for (int i = 0; i < bones.Length; i++) bones[i] = MapBoneReference(bones[i]);
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

        private Transform MapBoneReference(Transform bone)
        {
            if (bone != null && BoneRemappings.TryGetValue(bone, out var newBone))
            {
                BoneDatabase.MarkNonRetargetable(newBone);
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

            if (zipMerge) BoneDatabase.AddMergedBone(mergedSrcBone.transform);
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