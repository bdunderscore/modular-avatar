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
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MergeArmatureHook
    {
        private BuildContext context;

        private Dictionary<Transform, Transform> BoneRemappings = new Dictionary<Transform, Transform>();
        private HashSet<GameObject> ToDelete = new HashSet<GameObject>();
        private HashSet<IConstraint> AddedConstraints = new HashSet<IConstraint>();

        internal bool OnPreprocessAvatar(BuildContext context, GameObject avatarGameObject)
        {
            this.context = context;

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

            new RetargetMeshes().OnPreprocessAvatar(avatarGameObject);

            //foreach (var bone in ToDelete) UnityEngine.Object.DestroyImmediate(bone);

            return true;
        }

        private void FixupConstraint(IConstraint constraint)
        {
            int nSources = constraint.sourceCount;
            for (int i = 0; i < nSources; i++)
            {
                var source = constraint.GetSource(i);
                source.sourceTransform = MapConstraintSource(source.sourceTransform);
                ClearToDeleteFlag(source.sourceTransform);
                constraint.SetSource(i, source);
            }

            if (constraint is AimConstraint aimConstraint)
            {
                aimConstraint.worldUpObject = MapConstraintSource(aimConstraint.worldUpObject);
                ClearToDeleteFlag(aimConstraint.worldUpObject);
            }

            if (constraint is LookAtConstraint lookAtConstraint)
            {
                lookAtConstraint.worldUpObject = MapConstraintSource(lookAtConstraint.worldUpObject);
                ClearToDeleteFlag(lookAtConstraint.worldUpObject);
            }
        }

        private Transform MapConstraintSource(Transform transform)
        {
            if (transform == null) return null;
            if (!BoneRemappings.TryGetValue(transform, out var remap)) return transform;
            var retarget = BoneDatabase.GetRetargetedBone(remap);
            return retarget != null ? retarget : remap;
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
            BoneDatabase.MarkNonRetargetable(t);
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

        struct IntermediateObj
        {
            public string name;
            public string originPath;
            public bool active;
        }

        private List<IntermediateObj> intermediateObjects = new List<IntermediateObj>();
        private Dictionary<string, List<string>> activationPathMappings = new Dictionary<string, List<string>>();

        private void MergeArmature(ModularAvatarMergeArmature mergeArmature)
        {
            // TODO: error reporting framework?
            if (mergeArmature.mergeTargetObject == null) return;

            GatherActiveStatePaths(mergeArmature.transform);

            RecursiveMerge(mergeArmature, mergeArmature.gameObject, mergeArmature.mergeTargetObject.gameObject, true);

            FixupAnimations();
        }

        private AnimationCurve GetActiveBinding(AnimationClip clip, string path)
        {
            return AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"));
        }

        private void FixupAnimations()
        {
            foreach (var kvp in activationPathMappings)
            {
                var path = kvp.Key;
                var mappings = kvp.Value;

                foreach (var holder in context.AnimationDatabase.ClipsForPath(path))
                {
                    if (!Util.IsTemporaryAsset(holder.CurrentClip))
                    {
                        holder.CurrentClip = Object.Instantiate(holder.CurrentClip);
                    }

                    var clip = holder.CurrentClip as AnimationClip;
                    if (clip == null) continue;

                    var curve = GetActiveBinding(clip, path);
                    if (curve != null)
                    {
                        foreach (var mapping in mappings)
                        {
                            clip.SetCurve(mapping, typeof(GameObject), "m_IsActive", curve);
                        }
                    }
                }
            }
        }

        private void GatherActiveStatePaths(Transform root)
        {
            intermediateObjects.Clear();
            activationPathMappings.Clear();

            List<IntermediateObj> rootPath = new List<IntermediateObj>();

            while (root != null && root.GetComponent<VRCAvatarDescriptor>() == null)
            {
                rootPath.Insert(0, new IntermediateObj()
                {
                    name = root.name,
                    originPath = RuntimeUtil.AvatarRootPath(root.gameObject),
                    active = root.gameObject.activeSelf
                });
                root = root.parent;
            }

            var prefix = "";

            for (int i = 1; i <= rootPath.Count; i++)
            {
                var srcPrefix = string.Join("/", rootPath.Take(i).Select(p => p.name));
                if (context.AnimationDatabase.ClipsForPath(srcPrefix).Any(clip =>
                        GetActiveBinding(clip.CurrentClip as AnimationClip, srcPrefix) != null
                    ))
                {
                    var intermediate = rootPath[i - 1].name + "$" + Guid.NewGuid().ToString();
                    var originPath = rootPath[i - 1].originPath;
                    intermediateObjects.Add(new IntermediateObj()
                    {
                        name = intermediate,
                        originPath = originPath,
                        active = rootPath[i - 1].active
                    });
                    if (prefix.Length > 0) prefix += "/";
                    prefix += intermediate;
                    activationPathMappings[originPath] = new List<string>();
                }
            }
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
            if (src == newParent)
            {
                throw new Exception("[ModularAvatar] Attempted to merge an armature into itself! Aborting build...");
            }

            if (zipMerge) PruneDuplicatePhysBones(newParent, src);

            bool retain = HasAdditionalComponents(src, out var constraintType) || !zipMerge;
            zipMerge = zipMerge && constraintType == null;

            GameObject mergedSrcBone = newParent;
            if (retain)
            {
                mergedSrcBone = newParent;
                var switchPath = "";
                foreach (var intermediate in intermediateObjects)
                {
                    var preexisting = mergedSrcBone.transform.Find(intermediate.name);
                    if (preexisting != null)
                    {
                        mergedSrcBone = preexisting.gameObject;
                        continue;
                    }

                    var switchObj = new GameObject(intermediate.name);
                    switchObj.transform.SetParent(mergedSrcBone.transform, false);
                    switchObj.transform.localPosition = Vector3.zero;
                    switchObj.transform.localRotation = Quaternion.identity;
                    switchObj.transform.localScale = Vector3.one;
                    switchObj.SetActive(intermediate.active);

                    if (switchPath.Length > 0)
                    {
                        switchPath += "/";
                    }
                    else
                    {
                        // This new leaf can break parent bone physbones. Add a PB Blocker
                        // to prevent this becoming an issue.
                        switchObj.GetOrAddComponent<ModularAvatarPBBlocker>();
                    }

                    switchPath += intermediate.name;

                    activationPathMappings[intermediate.originPath].Add(RuntimeUtil.AvatarRootPath(switchObj));

                    mergedSrcBone = switchObj;

                    // Ensure mesh retargeting looks through this 
                    BoneDatabase.AddMergedBone(mergedSrcBone.transform);
                }
            }

            var oldPath = RuntimeUtil.AvatarRootPath(src);
            src.transform.SetParent(mergedSrcBone.transform, true);
            src.name = src.name + "$" + Guid.NewGuid();
            src.GetOrAddComponent<ModularAvatarPBBlocker>();
            mergedSrcBone = src;
            var newPath = RuntimeUtil.AvatarRootPath(src);

            PathMappings.Remap(oldPath, new PathMappings.MappingEntry()
            {
                path = zipMerge ? RuntimeUtil.AvatarRootPath(newParent) : newPath,
                transformPath = oldPath,
            });

            if (zipMerge)
            {
                BoneDatabase.AddMergedBone(src.transform);
            }
            else
            {
                // unmergable bones cannot be deleted
                retain = true;
            }

            List<Transform> children = new List<Transform>();
            foreach (Transform child in src.transform)
            {
                children.Add(child);
            }

            if (zipMerge)
            {
                foreach (Transform child in children)
                {
                    var childGameObject = child.gameObject;
                    var childName = childGameObject.name;
                    GameObject childNewParent = mergedSrcBone;
                    bool shouldZip = false;

                    if (childName.StartsWith(config.prefix) && childName.EndsWith(config.suffix))
                    {
                        var targetObjectName = childName.Substring(config.prefix.Length,
                            childName.Length - config.prefix.Length - config.suffix.Length);
                        var targetObject = newParent.transform.Find(targetObjectName);
                        if (targetObject != null)
                        {
                            childNewParent = targetObject.gameObject;
                            shouldZip = true;
                        }
                    }

                    RecursiveMerge(config, childGameObject, childNewParent, shouldZip);
                }
            }

            if (!retain) ToDelete.Add(src);

            return retain;
        }

        /**
         * Sometimes outfit authors copy the entire armature, including PhysBones components. If we merge these and
         * end up with multiple PB components referencing the same target, PB refuses to animate the bone. So detect
         * and prune this case.
         *
         * For simplicity - we currently only detect the case where the physbone references the component it's on.
         * TODO - detect duplicate colliders, contacts, et - these can cause perf issues but usually not quite as large
         * of a correctness issue.
         */
        private void PruneDuplicatePhysBones(GameObject baseBone, GameObject mergeBone)
        {
            bool hasSelfReferencePB = false;

            foreach (var pb in baseBone.GetComponents<VRCPhysBone>())
            {
                var target = pb.rootTransform;
                if (target == null || target == baseBone.transform)
                {
                    hasSelfReferencePB = true;
                    break;
                }
            }

            if (!hasSelfReferencePB) return;

            foreach (var pb in mergeBone.GetComponents<VRCPhysBone>())
            {
                var target = pb.rootTransform;
                if (target == null || target == baseBone.transform)
                {
                    Object.DestroyImmediate(pb);
                }
            }
        }
    }
}