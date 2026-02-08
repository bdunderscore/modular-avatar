#if MA_VRCSDK3_AVATARS
#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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

    internal class VisibleHeadAccessoryProcessorState
    {
        public BuildContext Context;
        public VisibleHeadAccessoryValidation Validator;
        public Transform AvatarTransform;
        public HashSet<Transform> VisibleBones = new HashSet<Transform>();
        public Transform ProxyHead;
        public Dictionary<Transform, Transform> BoneShims = new Dictionary<Transform, Transform>();
    }
    
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class VisibleHeadAccessoryProcessor : Pass<VisibleHeadAccessoryProcessor>
    {
        private const double EPSILON = 0.01;

        protected override void Execute(ndmf.BuildContext context)
        {
            var buildContext = context.Extension<BuildContext>();
            var state = context.GetState<VisibleHeadAccessoryProcessorState>();
            
            state.Context = buildContext;
            state.AvatarTransform = buildContext.AvatarRootTransform;
            state.Validator = new VisibleHeadAccessoryValidation(buildContext.AvatarRootObject);

            Process(state);
        }

        private void Process(VisibleHeadAccessoryProcessorState state)
        {
            bool didWork = false;

            foreach (var target in state.AvatarTransform.GetComponentsInChildren<ModularAvatarVisibleHeadAccessory>(true))
            {
                var w = BuildReport.ReportingObject(target, () => ProcessComponent(target, state));
                didWork = didWork || w;
            }

            if (didWork)
            {
                // Process meshes
                foreach (var smr in state.AvatarTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null) continue;

                    BuildReport.ReportingObject(smr,
                        () => new VisibleHeadAccessoryMeshProcessor(smr, state.VisibleBones, state.ProxyHead).Retarget(state.Context));
                }
            }
        }

        bool ProcessComponent(ModularAvatarVisibleHeadAccessory target, VisibleHeadAccessoryProcessorState state)
        {
            bool didWork = false;
            
            if (state.Validator.Validate(target) == VisibleHeadAccessoryValidation.ReadyStatus.Ready)
            {
                var shim = CreateShim(target.transform.parent, state);

                target.transform.SetParent(shim, true);

                didWork = true;
            }

            if (didWork)
            {
                foreach (var xform in target.GetComponentsInChildren<Transform>(true))
                {
                    state.VisibleBones.Add(xform);
                }

                ProcessAnimations(state);
            }

            Object.DestroyImmediate(target);

            return didWork;
        }

        private void ProcessAnimations(VisibleHeadAccessoryProcessorState state)
        {
            var animdb = state.Context.PluginBuildContext.Extension<AnimatorServicesContext>();
            var paths = animdb.ObjectPathRemapper;
            Dictionary<string, string> pathMappings = new Dictionary<string, string>();
            HashSet<VirtualClip> clips = new();

            foreach (var kvp in state.BoneShims)
            {
                var orig = paths.GetVirtualPathForObject(kvp.Key.gameObject);
                var shim = paths.GetVirtualPathForObject(kvp.Value.gameObject);

                pathMappings[orig] = shim;

                clips.UnionWith(animdb.AnimationIndex.GetClipsForObjectPath(orig));
            }

            foreach (var clip in clips)
            {
                foreach (var binding in clip.GetFloatCurveBindings())
                {
                    if (binding.type == typeof(Transform) && pathMappings.TryGetValue(binding.path, out var newPath))
                    {
                        clip.SetFloatCurve(
                            EditorCurveBinding.FloatCurve(newPath, typeof(Transform), binding.propertyName),
                            clip.GetFloatCurve(binding)
                        );
                    }
                }
            }
        }

        private Transform CreateShim(Transform target, VisibleHeadAccessoryProcessorState state)
        {
            if (state.BoneShims.TryGetValue(target.transform, out var shim)) return shim;

            if (target == state.Validator.HeadBone) return CreateProxy(state);
            if (target.parent == null)
            {
                // parent is not the head bone...?
                throw new ArgumentException("Failed to find head bone");
            }

            var parentShim = CreateShim(target.parent, state);

            GameObject obj = new GameObject(target.gameObject.name);
            obj.transform.SetParent(parentShim, false);
            obj.transform.localPosition = target.localPosition;
            obj.transform.localRotation = target.localRotation;
            obj.transform.localScale = target.localScale;

            state.BoneShims[target] = obj.transform;

            return obj.transform;
        }

        private Transform CreateProxy(VisibleHeadAccessoryProcessorState state)
        {
            if (state.ProxyHead != null) return state.ProxyHead;

            var src = state.Validator.HeadBone;
            var obj = new GameObject(src.name + " (HeadChop)");

            var parent = state.Validator.HeadBone;

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

            state.ProxyHead = obj.transform;

            // TODO - lock proxy scale to head scale in animation?

            return obj.transform;
        }
    }
}

#endif