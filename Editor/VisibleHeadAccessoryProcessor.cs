#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEditor;
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
        private const double EPSILON = 0.01;

        private BuildContext _context;
        private VisibleHeadAccessoryValidation _validator;
        
        private Transform _avatarTransform;
        private ImmutableHashSet<Transform> _activeBones => _validator.ActiveBones;
        private Transform _headBone => _validator.HeadBone;

        private HashSet<Transform> _visibleBones = new HashSet<Transform>();
        private Transform _proxyHead;

        private Dictionary<Transform, Transform> _boneShims = new Dictionary<Transform, Transform>();

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
            bool didWork = false;
            
            if (_validator.Validate(target) == VisibleHeadAccessoryValidation.ReadyStatus.Ready)
            {
                var shim = CreateShim(target.transform.parent);

                target.transform.SetParent(shim, true);

                didWork = true;
            }

            if (didWork)
            {
                foreach (var xform in target.GetComponentsInChildren<Transform>(true))
                {
                    _visibleBones.Add(xform);
                }

                ProcessAnimations();
            }

            Object.DestroyImmediate(target);

            return didWork;
        }

        private void ProcessAnimations()
        {
            var animdb = _context.AnimationDatabase;
            var paths = _context.PathMappings;
            Dictionary<string, string> pathMappings = new Dictionary<string, string>();

            foreach (var kvp in _boneShims)
            {
                var orig = paths.GetObjectIdentifier(kvp.Key.gameObject);
                var shim = paths.GetObjectIdentifier(kvp.Value.gameObject);

                pathMappings[orig] = shim;
            }

            animdb.ForeachClip(motion =>
            {
                if (!(motion.CurrentClip is AnimationClip clip)) return;

                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    if (binding.type != typeof(Transform)) continue;
                    if (!pathMappings.TryGetValue(binding.path, out var newPath)) continue;

                    var newBinding = binding;
                    newBinding.path = newPath;
                    AnimationUtility.SetEditorCurve(clip, newBinding, AnimationUtility.GetEditorCurve(clip, binding));
                }
            });
        }

        private Transform CreateShim(Transform target)
        {
            if (_boneShims.TryGetValue(target.transform, out var shim)) return shim;

            if (target == _headBone) return CreateProxy();
            if (target.parent == null)
            {
                // parent is not the head bone...?
                throw new ArgumentException("Failed to find head bone");
            }

            var parentShim = CreateShim(target.parent);

            GameObject obj = new GameObject(target.gameObject.name);
            obj.transform.SetParent(parentShim, false);
            obj.transform.localPosition = target.localPosition;
            obj.transform.localRotation = target.localRotation;
            obj.transform.localScale = target.localScale;

            _boneShims[target] = obj.transform;

            return obj.transform;
        }

        private Transform CreateProxy()
        {
            if (_proxyHead != null) return _proxyHead;

            var src = _headBone;
            var obj = new GameObject(src.name + " (HeadChop)");

            var parent = _headBone;

            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = src.localPosition;
            obj.transform.localRotation = src.localRotation;
            obj.transform.localScale = src.localScale;
            Debug.Log($"src.localScale = {src.localScale} obj.transform.localScale = {obj.transform.localScale}");

            var headChop = obj.AddComponent<VRCHeadChop>();
            headChop.targetBones = new[]
            {
                new VRCHeadChop.HeadChopBone
                {
                    transform = obj.transform,
                    applyCondition = VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply,
                    scaleFactor = 1
                }
            };
            headChop.globalScaleFactor = 1;

            _proxyHead = obj.transform;

            // TODO - lock proxy scale to head scale in animation?

            return obj.transform;
        }
    }
}
