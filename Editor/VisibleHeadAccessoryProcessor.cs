using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEngine;
using UnityEngine.Animations;

#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

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
        private const double EPSILON = 0.01;

        private BuildContext _context;
        private VisibleHeadAccessoryValidation _validator;
        
        private Transform _avatarTransform;
        private ImmutableHashSet<Transform> _activeBones => _validator.ActiveBones;
        private Transform _headBone => _validator.HeadBone;

        private HashSet<Transform> _visibleBones = new HashSet<Transform>();
        private Transform _proxyHead;

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
                // Process meshes
                foreach (var smr in _avatarTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    BuildReport.ReportingObject(smr,
                        () => new VisibleHeadAccessoryMeshProcessor(smr, _visibleBones, _proxyHead).Retarget(_context));
                }
            }
        }

        bool Process(ModularAvatarVisibleHeadAccessory target)
        {
#if UNITY_ANDROID
            Object.DestroyImmediate(target);
            return false;
#endif

            bool didWork = false;
            
            if (_validator.Validate(target) == VisibleHeadAccessoryValidation.ReadyStatus.Ready)
            {
                var proxy = CreateProxy();

                target.transform.SetParent(proxy, true);

                didWork = true;
            }

            if (didWork)
            {
                foreach (var xform in target.GetComponentsInChildren<Transform>(true))
                {
                    _visibleBones.Add(xform);
                }
            }

            Object.DestroyImmediate(target);

            return didWork;
        }

        private Transform CreateProxy()
        {
            if (_proxyHead != null) return _proxyHead;

            var src = _headBone;
            GameObject obj = new GameObject(src.name + " (FirstPersonVisible)");

            Transform parent = _headBone.parent;

            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = src.localPosition;
            obj.transform.localRotation = src.localRotation;
            obj.transform.localScale = src.localScale;
            Debug.Log($"src.localScale = {src.localScale} obj.transform.localScale = {obj.transform.localScale}");

            var constraint = obj.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource()
            {
                weight = 1.0f,
                sourceTransform = src
            });
            constraint.constraintActive = true;
            constraint.locked = true;
            constraint.rotationOffsets = new[] {Vector3.zero};
            constraint.translationOffsets = new[] {Vector3.zero};

            _proxyHead = obj.transform;

            // TODO - lock proxy scale to head scale in animation?

            return obj.transform;
        }
    }
}
