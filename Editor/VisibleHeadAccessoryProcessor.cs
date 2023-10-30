﻿using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class VisibleHeadAccessoryProcessor
    {
        private const double EPSILON = 0.01;

        internal enum ReadyStatus
        {
            Ready,
            ParentMarked,
            NotUnderHead,
            InPhysBoneChain
        }

        private BuildContext _context;
        private Transform _avatarTransform;
        private HashSet<Transform> _activeBones = new HashSet<Transform>();
        private Transform _headBone;

        private HashSet<Transform> _visibleBones = new HashSet<Transform>();
        private Transform _proxyHead;

        public VisibleHeadAccessoryProcessor(BuildContext context)
        {
            _context = context;
            _avatarTransform = context.AvatarRootTransform;

            var animator = _avatarTransform.GetComponent<Animator>();
            _headBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

            foreach (var physBone in _avatarTransform.GetComponentsInChildren<VRCPhysBone>(true))
            {
                var boneRoot = physBone.rootTransform != null ? physBone.rootTransform : physBone.transform;
                var ignored = new HashSet<Transform>(physBone.ignoreTransforms);

                foreach (Transform child in boneRoot)
                {
                    Traverse(child, ignored);
                }
            }

            void Traverse(Transform bone, HashSet<Transform> ignored)
            {
                if (ignored.Contains(bone)) return;
                _activeBones.Add(bone);

                foreach (Transform child in bone)
                {
                    Traverse(child, ignored);
                }
            }
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
            foreach (var target in _avatar.GetComponentsInChildren<ModularAvatarVisibleHeadAccessory>(true))
            {
                Object.DestroyImmediate(target);
            }
            return;
#endif
            bool didWork = false;

            if (Validate(target) == ReadyStatus.Ready)
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

        internal ReadyStatus Validate(ModularAvatarVisibleHeadAccessory target)
        {
            ReadyStatus status = ReadyStatus.NotUnderHead;
            Transform node = target.transform.parent;

            if (_activeBones.Contains(target.transform)) return ReadyStatus.InPhysBoneChain;

            while (node != null)
            {
                if (node.GetComponent<ModularAvatarVisibleHeadAccessory>()) return ReadyStatus.ParentMarked;
                if (_activeBones.Contains(node)) return ReadyStatus.InPhysBoneChain;

                if (node == _headBone)
                {
                    status = ReadyStatus.Ready;
                    break;
                }

                node = node.parent;
            }

            return status;
        }
    }
}
