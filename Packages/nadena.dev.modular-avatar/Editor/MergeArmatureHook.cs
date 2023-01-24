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
        private HashSet<Transform> mergedObjects = new HashSet<Transform>();
        private HashSet<Transform> thisPassAdded = new HashSet<Transform>();

        internal void OnPreprocessAvatar(BuildContext context, GameObject avatarGameObject)
        {
            this.context = context;

            var mergeArmatures = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeArmature>(true);

            foreach (var mergeArmature in mergeArmatures)
            {
                mergedObjects.Clear();
                thisPassAdded.Clear();
                MergeArmature(mergeArmature);
                PruneDuplicatePhysBones();
                UnityEngine.Object.DestroyImmediate(mergeArmature);
            }

            foreach (var renderer in avatarGameObject.transform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = renderer.bones;
                renderer.bones = bones;
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                RetainBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBoneCollider>(true))
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                RetainBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<ContactBase>(true))
            {
                if (c.rootTransform == null) c.rootTransform = c.transform;
                RetainBoneReferences(c);
            }

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<IConstraint>(true))
            {
                RetainBoneReferences(c as Component);
            }

            new RetargetMeshes().OnPreprocessAvatar(avatarGameObject, context);
        }

        private void RetainBoneReferences(Component c)
        {
            if (c == null) return;

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
                            BoneDatabase.RetainMergedBone(t);
                        }
                        else if (iter.objectReferenceValue is GameObject go)
                        {
                            BoneDatabase.RetainMergedBone(go.transform);
                        }

                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private bool HasAdditionalComponents(GameObject go)
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
                    default:
                        hasComponents = true;
                        break;
                }
            }

            return hasComponents;
        }

        /// <summary>
        /// Tracks an object whose Active state is animated, and which leads up to this Merge Animator component.
        /// We use this tracking data to create proxy objects within the main armature, which track the same active
        /// state.
        /// </summary>
        struct IntermediateObj
        {
            /// <summary>
            /// Name of the intermediate object. Used to name proxy objects.
            /// </summary>
            public string name;

            /// <summary>
            /// The original path of this intermediate object.
            /// </summary>
            public string originPath;

            /// <summary>
            /// Whether this object is initially active.
            /// </summary>
            public bool active;
        }

        private List<IntermediateObj> intermediateObjects = new List<IntermediateObj>();

        private Dictionary<string, List<GameObject>>
            activationPathMappings = new Dictionary<string, List<GameObject>>();

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
                            clip.SetCurve(PathMappings.GetObjectIdentifier(mapping), typeof(GameObject), "m_IsActive",
                                curve);
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
                    activationPathMappings[originPath] = new List<GameObject>();
                }
            }
        }

        /**
         * (Attempts to) merge the source gameobject into the target gameobject. Returns true if the merged source
         * object must be retained.
         */
        private void RecursiveMerge(ModularAvatarMergeArmature config,
            GameObject src,
            GameObject newParent,
            bool zipMerge)
        {
            if (src == newParent)
            {
                throw new Exception("[ModularAvatar] Attempted to merge an armature into itself! Aborting build...");
            }

            if (zipMerge)
            {
                mergedObjects.Add(src.transform);
                thisPassAdded.Add(src.transform);
            }

            bool retain = HasAdditionalComponents(src) || !zipMerge;
            zipMerge = zipMerge && src.GetComponent<IConstraint>() == null;

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

                    activationPathMappings[intermediate.originPath].Add(switchObj);

                    mergedSrcBone = switchObj;

                    // Ensure mesh retargeting looks through this 
                    BoneDatabase.AddMergedBone(mergedSrcBone.transform);
                    BoneDatabase.RetainMergedBone(mergedSrcBone.transform);
                    PathMappings.MarkTransformLookthrough(mergedSrcBone);
                    thisPassAdded.Add(mergedSrcBone.transform);
                }
            }

            src.transform.SetParent(mergedSrcBone.transform, true);
            src.name = src.name + "$" + Guid.NewGuid();
            src.GetOrAddComponent<ModularAvatarPBBlocker>();
            mergedSrcBone = src;

            if (zipMerge)
            {
                PathMappings.MarkTransformLookthrough(src);
                BoneDatabase.AddMergedBone(src.transform);
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
        }

        Transform FindOriginalParent(Transform merged)
        {
            while (merged != null && thisPassAdded.Contains(merged)) merged = merged.parent;
            return merged;
        }

        /**
         * Sometimes outfit authors copy the entire armature, including PhysBones components. If we merge these and
         * end up with multiple PB components referencing the same target, PB refuses to animate the bone. So detect
         * and prune this case.
         *
         * TODO - detect duplicate colliders, contacts, et - these can cause perf issues but usually not quite as large
         * of a correctness issue.
         */
        private void PruneDuplicatePhysBones()
        {
            foreach (var obj in mergedObjects)
            {
                if (obj.GetComponent<VRCPhysBone>() == null) continue;
                var baseObj = FindOriginalParent(obj);
                if (baseObj == null || baseObj.GetComponent<VRCPhysBone>() == null) continue;

                HashSet<Transform> baseTargets = new HashSet<Transform>();
                foreach (var component in baseObj.GetComponents<VRCPhysBone>())
                {
                    var target = component.rootTransform == null ? baseObj.transform : component.rootTransform;
                    baseTargets.Add(target);
                }

                foreach (var component in obj.GetComponents<VRCPhysBone>())
                {
                    var target = component.rootTransform == null
                        ? baseObj.transform
                        : FindOriginalParent(component.rootTransform);
                    if (baseTargets.Contains(target))
                    {
                        Object.DestroyImmediate(component);
                    }
                    else
                    {
                        BoneDatabase.RetainMergedBone(component.transform);
                    }
                }
            }
        }
    }
}