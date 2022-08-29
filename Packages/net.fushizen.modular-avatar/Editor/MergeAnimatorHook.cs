using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace net.fushizen.modular_avatar.core.editor
{
    public class MergeAnimatorHook : IVRCSDKPreprocessAvatarCallback
    {
        private const string SAMPLE_PATH_PACKAGE = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";
        private const string SAMPLE_PATH_LEGACY = "Assets/VRCSDK/Examples3/Animation/Controllers";
        
        public int callbackOrder => HookSequence.SEQ_MERGE_ANIMATORS;
        
        Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorCombiner> mergeSessions =
            new Dictionary<VRCAvatarDescriptor.AnimLayerType, AnimatorCombiner>();
        
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var descriptor = avatarGameObject.GetComponent<VRCAvatarDescriptor>();

            InitSessions(descriptor.baseAnimationLayers);
            InitSessions(descriptor.specialAnimationLayers);
            
            var toMerge = avatarGameObject.transform.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);

            foreach (var merge in toMerge)
            {
                if (merge.animator != null && mergeSessions.TryGetValue(merge.layerType, out var session))
                {
                    var relativePath = RuntimeUtil.RelativePath(avatarGameObject, merge.gameObject);
                    mergeSessions[merge.layerType].AddController(
                        relativePath != "" ? relativePath + "/" : "",
                        (AnimatorController) merge.animator
                    );
                }

                if (merge.deleteAttachedAnimator)
                {
                    var animator = merge.GetComponent<Animator>();
                    if (animator != null) Object.DestroyImmediate(animator);
                }
            }

            descriptor.baseAnimationLayers = FinishSessions(descriptor.baseAnimationLayers);
            descriptor.specialAnimationLayers = FinishSessions(descriptor.specialAnimationLayers);

            return true;
        }

        private VRCAvatarDescriptor.CustomAnimLayer[] FinishSessions(
            VRCAvatarDescriptor.CustomAnimLayer[] layers
        )
        {
            layers = (VRCAvatarDescriptor.CustomAnimLayer[]) layers.Clone();

            for (int i = 0; i < layers.Length; i++)
            {
                layers[i].isDefault = false;
                layers[i].animatorController = mergeSessions[layers[i].type].Finish();
            }

            return layers;
        }

        private void InitSessions(VRCAvatarDescriptor.CustomAnimLayer[] layers)
        {
            foreach (var layer in layers)
            {
                var controller = ResolveLayerController(layer);
                if (controller == null) controller = new AnimatorController();

                var session = new AnimatorCombiner();
                session.AddController("", controller);
                
                mergeSessions[layer.type] = session;
            }
        }
        
        

        private static AnimatorController ResolveLayerController(VRCAvatarDescriptor.CustomAnimLayer layer)
        {
            AnimatorController controller = null;

            if (!layer.isDefault && layer.animatorController != null &&
                layer.animatorController is AnimatorController c)
            {
                controller = c;
            }
            else
            {
                string name;
                switch (layer.type)
                {
                    case VRCAvatarDescriptor.AnimLayerType.Action:
                        name = "Action";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Additive:
                        name = "Idle";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Base:
                        name = "Locomotion";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Gesture:
                        name = "Hands";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Sitting:
                        name = "Sitting";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.FX:
                        name = "Face";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.TPose:
                        name = "UtilityTPose";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.IKPose:
                        name = "UtilityIKPose";
                        break;
                    default:
                        name = null;
                        break;
                }

                if (name != null)
                {
                    name = "/vrc_AvatarV3" + name + "Layer.controller";

                    controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SAMPLE_PATH_PACKAGE + name);
                    if (controller == null)
                    {
                        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SAMPLE_PATH_LEGACY + name);
                    }
                }
            }

            return controller;
        }
    }
}