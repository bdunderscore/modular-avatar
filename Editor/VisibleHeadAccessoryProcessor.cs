#if MA_VRCSDK3_AVATARS
#region

using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class VisibleHeadAccessoryValidation
    {
        internal ImmutableHashSet<Transform> ActiveBones { get;  }
        internal Transform HeadBone { get; }

        internal enum ReadyStatus
        {
            Ready,
            ParentMarked,
            NotUnderHead,
            InPhysBoneChain
        }

        public VisibleHeadAccessoryValidation(GameObject avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            HeadBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            
            var activeBones = ImmutableHashSet.CreateBuilder<Transform>();
#if MA_VRCSDK3_AVATARS
            foreach (var physBone in avatarRoot.GetComponentsInChildren<VRCPhysBone>(true))
            {
                var boneRoot = physBone.rootTransform != null ? physBone.rootTransform : physBone.transform;
                var ignored = new HashSet<Transform>(physBone.ignoreTransforms);

                foreach (Transform child in boneRoot)
                {
                    Traverse(child, ignored);
                }
            }

            ActiveBones = activeBones.ToImmutable();

            void Traverse(Transform bone, HashSet<Transform> ignored)
            {
                if (ignored.Contains(bone)) return;
                activeBones.Add(bone);

                foreach (Transform child in bone)
                {
                    Traverse(child, ignored);
                }
            }
#endif
        }

        internal ReadyStatus Validate(ModularAvatarVisibleHeadAccessory target)
        {
            ReadyStatus status = ReadyStatus.NotUnderHead;
            Transform node = target.transform.parent;

            if (ActiveBones.Contains(target.transform)) return ReadyStatus.InPhysBoneChain;

            while (node != null)
            {
                if (node.GetComponent<ModularAvatarVisibleHeadAccessory>()) return ReadyStatus.ParentMarked;
                if (ActiveBones.Contains(node)) return ReadyStatus.InPhysBoneChain;

                if (node == HeadBone)
                {
                    status = ReadyStatus.Ready;
                    break;
                }

                node = node.parent;
            }

            return status;
        }
    }
    
    internal class VisibleHeadAccessoryProcessor
    {
        private BuildContext _context;
        private VisibleHeadAccessoryValidation _validator;
        
        private Transform _avatarTransform;
        private ImmutableHashSet<Transform> _activeBones => _validator.ActiveBones;
        private Transform _headBone => _validator.HeadBone;

        private HashSet<Transform> _visibleBones = new HashSet<Transform>();
        private List<Transform> _headChopTargets = new List<Transform>();

        public VisibleHeadAccessoryProcessor(BuildContext context)
        {
            _context = context;
            _avatarTransform = context.AvatarRootTransform;
            
            _validator = new VisibleHeadAccessoryValidation(context.AvatarRootObject);
        }

        public void Process()
        {
            bool didWork = false;

            foreach (var target in _avatarTransform.GetComponentsInChildren<ModularAvatarVisibleHeadAccessory>(true))
            {
                var w = BuildReport.ReportingObject(target, () => Process(target));
                didWork = didWork || w;
            }

            if (didWork)
            {
                // Shared clone transform mapping (cross-mesh dedup)
                var cloneMappings = new Dictionary<Transform, Transform>();

                Transform RemapBone(Transform original)
                {
                    if (!cloneMappings.TryGetValue(original, out var clone))
                    {
                        clone = new GameObject(original.name + " (VHA Clone)").transform;
                        clone.SetParent(original, false);
                        clone.gameObject.AddComponent<ModularAvatarPBBlocker>();
                        _headChopTargets.Add(clone);
                        cloneMappings[original] = clone;
                    }

                    return clone;
                }

                // Process meshes
                foreach (var smr in _avatarTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;

                    var mp = new VisibleHeadAccessoryMeshProcessor(smr, _visibleBones, _context, RemapBone);
                    BuildReport.ReportingObject(smr, () => mp.Retarget());
                }

                // Create a single HeadChop referencing all targets
                if (_headChopTargets.Count > 0)
                {
                    var headChopObj = new GameObject("VHA HeadChop");
                    headChopObj.transform.SetParent(_avatarTransform, false);
                    var headChop = headChopObj.AddComponent<VRCHeadChop>();

                    var bones = new VRCHeadChop.HeadChopBone[_headChopTargets.Count];
                    for (int i = 0; i < _headChopTargets.Count; i++)
                    {
                        bones[i] = new VRCHeadChop.HeadChopBone
                        {
                            transform = _headChopTargets[i],
                            applyCondition = VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply,
                            scaleFactor = 1
                        };
                    }
                    headChop.targetBones = bones;
                    headChop.globalScaleFactor = 1;
                }
            }
        }

        bool Process(ModularAvatarVisibleHeadAccessory target)
        {
            bool didWork = false;
            
            if (_validator.Validate(target) == VisibleHeadAccessoryValidation.ReadyStatus.Ready)
            {
                // All transforms under the VHA component should be visible in first person
                foreach (var xform in target.GetComponentsInChildren<Transform>(true))
                {
                    _visibleBones.Add(xform);
                    _headChopTargets.Add(xform);
                }

                didWork = true;
            }

            Object.DestroyImmediate(target);

            return didWork;
        }
    }
}

#endif