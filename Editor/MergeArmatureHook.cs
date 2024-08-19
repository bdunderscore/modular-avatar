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

#region

#if MA_VRCSDK3_AVATARS
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class
        MergeArmatureHook
    {
        private const float DuplicatedBoneMaxSqrDistance = 0.001f * 0.001f;

        private ndmf.BuildContext frameworkContext;
        private BuildContext context;
#if MA_VRCSDK3_AVATARS
        private Dictionary<Transform, VRCPhysBoneBase> physBoneByRootBone;
#endif
        private BoneDatabase BoneDatabase = new BoneDatabase();

        private PathMappings PathMappings => frameworkContext.Extension<AnimationServicesContext>()
            .PathMappings;

        private HashSet<Transform> humanoidBones = new HashSet<Transform>();
        private HashSet<Transform> mergedObjects = new HashSet<Transform>();
        private HashSet<Transform> thisPassAdded = new HashSet<Transform>();

        internal void OnPreprocessAvatar(ndmf.BuildContext context, GameObject avatarGameObject)
        {
            this.frameworkContext = context;
            this.context = context.Extension<ModularAvatarContext>().BuildContext;
#if MA_VRCSDK3_AVATARS
            physBoneByRootBone = new Dictionary<Transform, VRCPhysBoneBase>();
            foreach (var physbone in avatarGameObject.transform.GetComponentsInChildren<VRCPhysBoneBase>(true))
                physBoneByRootBone[physbone.GetRootTransform()] = physbone;
#endif

            if (avatarGameObject.TryGetComponent<Animator>(out var animator) && animator.isHuman)
            {
                this.humanoidBones = new HashSet<Transform>(Enum.GetValues(typeof(HumanBodyBones))
                    .Cast<HumanBodyBones>()
                    .Where(x => x != HumanBodyBones.LastBone)
                    .Select(animator.GetBoneTransform));
            }

            var mergeArmatures =
                avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeArmature>(true);

            TopoProcessMergeArmatures(mergeArmatures);

#if MA_VRCSDK3_AVATARS
            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<ScaleProxy>(true))
            {
                BoneDatabase.AddMergedBone(c.transform);
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
#endif

#if MA_VRCSDK3_AVATARS_3_7_0_OR_NEWER
            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<VRCConstraintBase>(true))
            {
                RetainBoneReferences(c);
            }
#endif // MA_VRCSDK3_AVATARS_3_7_0_OR_NEWER

            foreach (var c in avatarGameObject.transform.GetComponentsInChildren<IConstraint>(true))
            {
                RetainBoneReferences(c as Component);
            }

            new RetargetMeshes().OnPreprocessAvatar(avatarGameObject, BoneDatabase, PathMappings);
        }

        private void TopoProcessMergeArmatures(ModularAvatarMergeArmature[] mergeArmatures)
        {
            Dictionary<ModularAvatarMergeArmature, List<ModularAvatarMergeArmature>> runsBefore
                = new Dictionary<ModularAvatarMergeArmature, List<ModularAvatarMergeArmature>>();

            foreach (var config in mergeArmatures)
            {
                var target = config.mergeTargetObject;
                if (target == null)
                {
                    // TODO - report error
                    continue;
                }

                var parentConfig = target.GetComponentInParent<ModularAvatarMergeArmature>();
                if (parentConfig != null)
                {
                    if (!runsBefore.ContainsKey(parentConfig))
                    {
                        runsBefore[parentConfig] = new List<ModularAvatarMergeArmature>();
                    }

                    runsBefore[parentConfig].Add(config);
                }
            }

            HashSet<ModularAvatarMergeArmature> visited = new HashSet<ModularAvatarMergeArmature>();
            Stack<ModularAvatarMergeArmature> visitStack = new Stack<ModularAvatarMergeArmature>();
            foreach (var next in mergeArmatures)
            {
                TopoLoop(next);
            }

            foreach (var next in mergeArmatures)
            {
                Object.DestroyImmediate(next);
            }

            void TopoLoop(ModularAvatarMergeArmature config)
            {
                if (visited.Contains(config)) return;
                if (visitStack.Contains(config))
                {
                    BuildReport.LogFatal("error.merge_armature.circular_dependency", new string[0], config);
                    return;
                }

                visitStack.Push(config);
                var target = config.mergeTargetObject;

                if (target != null)
                {
                    if (runsBefore.TryGetValue(config, out var predecessors))
                    {
                        foreach (var priorConfig in predecessors)
                        {
                            TopoLoop(priorConfig);
                        }
                    }

                    MergeArmatureWithReporting(config);
                }

                visitStack.Pop();
                visited.Add(config);
            }
        }

        private void MergeArmatureWithReporting(ModularAvatarMergeArmature config)
        {
            var target = config.mergeTargetObject;

            while (BoneDatabase.IsRetargetable(target.transform))
            {
                target = target.transform.parent.gameObject;
            }

            BuildReport.ReportingObject(config, () =>
            {
                mergedObjects.Clear();
                thisPassAdded.Clear();
                MergeArmature(config, target);
#if MA_VRCSDK3_AVATARS
                PruneDuplicatePhysBones();
#endif
            });
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

        private ActiveAnimationRetargeter _activeRetargeter;

        private void MergeArmature(ModularAvatarMergeArmature mergeArmature, GameObject mergeTargetObject)
        {
            // TODO: error reporting?
            if (mergeTargetObject == null) return;

            _activeRetargeter = new ActiveAnimationRetargeter(context, BoneDatabase, mergeArmature.transform);

            RecursiveMerge(mergeArmature, mergeArmature.gameObject, mergeTargetObject, true);

            _activeRetargeter.FixupAnimations();

            thisPassAdded.UnionWith(_activeRetargeter.AddedGameObjects.Select(x => x.transform));
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
                // Error reported by validation framework
                return;
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
                mergedSrcBone = _activeRetargeter.CreateIntermediateObjects(newParent);

            var isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(src.transform);
            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(src.transform);

            if (isPrefabAsset || isPrefabInstance)
            {
                throw new Exception("Cannot merge prefab instances or prefab assets");
            }

            if (mergedSrcBone == newParent
                && (
                    Vector3.SqrMagnitude(mergedSrcBone.transform.localScale - src.transform.localScale) > 0.00001f
                    || Quaternion.Angle(mergedSrcBone.transform.localRotation, src.transform.localRotation) > 0.00001f
                    || Vector3.SqrMagnitude(mergedSrcBone.transform.localPosition - src.transform.localPosition) >
                    0.00001f
                )
                && src.GetComponent<IConstraint>() != null
               )
            {
                // Constraints are sensitive to changes in local reference frames in some cases. In this case we'll
                // inject a dummy object in between in order to retain the local parent scale of the retargeted bone.
                var objName = src.gameObject.name + "$ConstraintRef " + Guid.NewGuid();

                var constraintScaleRef = new GameObject(objName);
                constraintScaleRef.transform.SetParent(src.transform.parent);
                constraintScaleRef.transform.localPosition = Vector3.zero;
                constraintScaleRef.transform.localRotation = Quaternion.identity;
                constraintScaleRef.transform.localScale = Vector3.one;

                constraintScaleRef.transform.SetParent(newParent.transform, true);
                mergedSrcBone = constraintScaleRef;

                BoneDatabase.AddMergedBone(mergedSrcBone.transform);
                BoneDatabase.RetainMergedBone(mergedSrcBone.transform);
                PathMappings.MarkTransformLookthrough(mergedSrcBone);
                thisPassAdded.Add(mergedSrcBone.transform);
            }

            src.transform.SetParent(mergedSrcBone.transform, true);
            if (config.mangleNames)
            {
                src.name = src.name + "$" + Guid.NewGuid();
            }

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
                    if (child.GetComponent <ModularAvatarMergeArmature>() != null)
                    {
                        continue;
                    }
                    
                    var childGameObject = child.gameObject;
                    var childName = childGameObject.name;
                    GameObject childNewParent = mergedSrcBone;
                    bool shouldZip = false;

                    if (childName.StartsWith(config.prefix) && childName.EndsWith(config.suffix)
                                                            && childName.Length > config.prefix.Length +
                                                            config.suffix.Length)
                    {
                        var targetObjectName = childName.Substring(config.prefix.Length,
                            childName.Length - config.prefix.Length - config.suffix.Length);
                        var targetObject = newParent.transform.Find(targetObjectName);
                        // Zip merge bones if the names match and the outfit side is not affected by its own PhysBone.
                        // Also zip merge when it seems to have been copied from avatar side by checking the dinstance.
                        if (targetObject != null)
                        {
                            if (NotAffectedByPhysBoneOrSimilarChainsAsTarget(child, targetObject))
                            {
                                childNewParent = targetObject.gameObject;
                                shouldZip = true;
                            }
                            else if (humanoidBones.Contains(targetObject))
                            {
                                BuildReport.LogFatal(
                                    "error.merge_armature.physbone_on_humanoid_bone", new string[0], config);
                            }
                        }
                    }

                    RecursiveMerge(config, childGameObject, childNewParent, shouldZip);
                }
            }
        }

        private bool NotAffectedByPhysBoneOrSimilarChainsAsTarget(Transform child, Transform target)
        {
#if MA_VRCSDK3_AVATARS
            // not affected
            if (!physBoneByRootBone.TryGetValue(child, out VRCPhysBoneBase physBone)) return true;

            var ignores = new HashSet<Transform>(physBone.ignoreTransforms.Where(x => x));

            return IsSimilarChainInPosition(child, target, ignores);
#else
            return IsSimilarChainInPosition(child, target, new HashSet<Transform>());
#endif
        }

        // Returns true if child and target are in similar position and children are recursively.
        private static bool IsSimilarChainInPosition(Transform child, Transform target, HashSet<Transform> ignores)
        {
            if ((target.position - child.position).sqrMagnitude > DuplicatedBoneMaxSqrDistance) return false;

            return child.Cast<Transform>()
                .Where(t => !ignores.Contains(t))
                .Select(t => (t, t2: target.Find(t.name)))
                .Where(t1 => t1.t2)
                .All(t1 => IsSimilarChainInPosition(t1.t, t1.t2, ignores));
        }

        Transform FindOriginalParent(Transform merged)
        {
            while (merged != null && thisPassAdded.Contains(merged)) merged = merged.parent;
            return merged;
        }

#if MA_VRCSDK3_AVATARS
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
#endif
    }
}