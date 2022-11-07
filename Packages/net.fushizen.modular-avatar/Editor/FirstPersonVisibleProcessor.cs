using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace net.fushizen.modular_avatar.core.editor
{
    internal class FirstPersonVisibleProcessor
    {
        private const double EPSILON = 0.01;

        internal enum ReadyStatus
        {
            Ready,
            ParentMarked,
            NotUnderHead,
            InPhysBoneChain
        }

        private VRCAvatarDescriptor _avatar;
        private HashSet<Transform> _activeBones = new HashSet<Transform>();
        private Transform _headBone;

        private Dictionary<Transform, Transform> _proxyBones = new Dictionary<Transform, Transform>();

        public FirstPersonVisibleProcessor(VRCAvatarDescriptor avatar)
        {
            _avatar = avatar;

            var animator = avatar.GetComponent<Animator>();
            _headBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

            foreach (var physBone in avatar.GetComponentsInChildren<VRCPhysBone>(true))
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
            foreach (var target in _avatar.GetComponentsInChildren<ModularAvatarFirstPersonVisible>(true))
            {
                Process(target);
            }
        }

        void Process(ModularAvatarFirstPersonVisible target)
        {
            if (Validate(target) == ReadyStatus.Ready)
            {
                var proxy = CreateProxy(_headBone);

                var xform = target.transform;

                var pscale = proxy.lossyScale;
                var oscale = xform.lossyScale;
                xform.localScale = new Vector3(oscale.x / pscale.x, oscale.y / pscale.y, oscale.z / pscale.z);

                target.transform.SetParent(proxy, true);
            }

            Object.DestroyImmediate(target);
        }

        private Transform CreateProxy(Transform src)
        {
            if (_proxyBones.TryGetValue(src, out var proxy)) return proxy;
            GameObject obj = new GameObject(src.name + " (FirstPersonVisible)");

            Transform parent = _headBone.parent;

            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = src.localPosition;
            obj.transform.localRotation = src.localRotation;
            obj.transform.localScale = src.localScale;

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

            _proxyBones.Add(src, obj.transform);

            return obj.transform;
        }

        internal ReadyStatus Validate(ModularAvatarFirstPersonVisible target)
        {
            ReadyStatus status = ReadyStatus.NotUnderHead;
            Transform node = target.transform.parent;

            if (_activeBones.Contains(target.transform)) return ReadyStatus.InPhysBoneChain;

            while (node != null)
            {
                if (node.GetComponent<ModularAvatarFirstPersonVisible>()) return ReadyStatus.ParentMarked;
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