#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
#if MA_VRCSDK3_AVATARS
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal sealed partial class UnityBlendTreeBackend
    {
        internal IMotionNode EmitAction(IAction action)
        {
            var clip = VirtualClip.Create("Effect " + action);
            switch (action)
            {
                case DriveActiveState active: EmitDriveActiveState(active, clip); break;
                case DriveParameter parameter: EmitDriveParameter(parameter, clip); break;
                case DriveInternalParameter internalParameter: EmitDriveInternalParameter(internalParameter, clip); break;
                case PropAction prop: EmitPropAction(prop, clip); break;
                case NullAction: break;
#if MA_VRCSDK3_AVATARS
                case NaNimationAction nanimation: EmitNaNimationAction(nanimation, clip); break;
#endif
                default:
                    Debug.LogWarning($"Unsupported action type: {action.GetType().FullName}");
                    break;
            }
            return new MotionNode(clip);
        }

        internal void ApplyBaseState(IAction action, bool actionStartsActive)
        {
            switch (action)
            {
                case DriveActiveState active: ApplyDriveActiveState(active, actionStartsActive); break;
                case DriveParameter parameter: ApplyDriveParameter(parameter, actionStartsActive); break;
                case DriveInternalParameter: break;
                case PropAction prop: ApplyPropAction(prop); break;
                case NullAction: break;
#if MA_VRCSDK3_AVATARS
                case NaNimationAction nanimation: ApplyNaNimationAction(nanimation, actionStartsActive); break;
#endif
                default:
                    Debug.LogWarning($"Unsupported action type: {action.GetType().FullName}");
                    break;
            }
        }

        internal void ApplyBaseState(TargetProp prop, float value)
        {
            if (prop.TargetObject is not Component component) return;

            BaseLayerClip.SetFloatCurve(
                EditorCurveBinding.FloatCurve(
                    ObjectPathRemapper.GetVirtualPathForObject(component.gameObject),
                    prop.TargetObject.GetType(),
                    prop.PropertyName),
                AnimationCurve.Constant(0, 1, value));
        }

        private void EmitDriveActiveState(DriveActiveState action, VirtualClip clip) => clip.SetFloatCurve(
            EditorCurveBinding.FloatCurve(ObjectPathRemapper.GetVirtualPathForObject(action.Target), typeof(GameObject), "m_IsActive"),
            AnimationCurve.Constant(0, 1, action.Active ? 1 : 0));

        private void ApplyDriveActiveState(DriveActiveState action, bool startsActive)
        {
            BaseLayerClip.SetFloatCurve(EditorCurveBinding.FloatCurve(ObjectPathRemapper.GetVirtualPathForObject(action.Target), typeof(GameObject), "m_IsActive"),
                AnimationCurve.Constant(0, 1, action.Target.activeSelf ? 1 : 0));
            if (startsActive) action.Target.SetActive(action.Active);
        }

        private static void EmitDriveParameter(DriveParameter action, VirtualClip clip) => clip.SetFloatCurve(
            EditorCurveBinding.FloatCurve("", typeof(Animator), action.ParameterName), AnimationCurve.Constant(0, 1, action.Value));

        private void ApplyDriveParameter(DriveParameter action, bool startsActive)
        {
            if (startsActive) SetParameterInitialValue(action.ParameterName, action.Value);
            else EnsureParameterPresent(action.ParameterName);
        }

        private static void EmitDriveInternalParameter(DriveInternalParameter action, VirtualClip clip) => clip.SetFloatCurve(
            EditorCurveBinding.FloatCurve("", typeof(Animator), action.ParameterName), AnimationCurve.Constant(0, 1, action.State ? 1 : 0));

        private void EmitPropAction(PropAction action, VirtualClip clip)
        {
            var binding = GetCurveBinding(action);
            if (!binding.HasValue) return;
            if (action.Value is float value) clip.SetFloatCurve(binding.Value, AnimationCurve.Constant(0, 1, value));
            else clip.SetObjectCurve(binding.Value, new[] { new ObjectReferenceKeyframe { time = 0, value = action.Value as Object } });
        }

        private EditorCurveBinding? GetCurveBinding(PropAction action)
        {
            var targetObject = action.Prop.TargetObject;
            var gameObject = targetObject switch { GameObject go => go, Component component => component.gameObject, _ => null };
            if (gameObject == null) return null;
            return action.Value is float
                ? EditorCurveBinding.FloatCurve(ObjectPathRemapper.GetVirtualPathForObject(gameObject), targetObject.GetType(), action.Prop.PropertyName)
                : EditorCurveBinding.PPtrCurve(ObjectPathRemapper.GetVirtualPathForObject(gameObject), targetObject.GetType(), action.Prop.PropertyName);
        }

        private void ApplyPropAction(PropAction action)
        {
            var binding = GetCurveBinding(action);
            if (!binding.HasValue) return;
            object? originalValue = null;
            var hasOriginalValue = false;
            var targetObject = action.Prop.TargetObject;
            if (targetObject is SkinnedMeshRenderer smr && action.Prop.PropertyName.StartsWith("blendShape."))
            {
                var mesh = smr.sharedMesh;
                if (mesh != null)
                {
                    var index = mesh.GetBlendShapeIndex(action.Prop.PropertyName[11..]);
                    if (index >= 0)
                    {
                        originalValue = smr.GetBlendShapeWeight(index);
                        hasOriginalValue = true;
                    }
                }
            }
            else
            {
                var property = new SerializedObject(targetObject).FindProperty(action.Prop.PropertyName);
                if (property != null)
                {
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Boolean: originalValue = property.boolValue ? 1.0f : 0.0f; hasOriginalValue = true; break;
                        case SerializedPropertyType.Float: originalValue = property.floatValue; hasOriginalValue = true; break;
                        case SerializedPropertyType.ObjectReference: originalValue = property.objectReferenceValue; hasOriginalValue = true; break;
                        default: return;
                    }
                }
            }
            if (originalValue is float value) BaseLayerClip.SetFloatCurve(binding.Value, AnimationCurve.Constant(0, 1, value));
            else if (hasOriginalValue) BaseLayerClip.SetObjectCurve(binding.Value, new[] { new ObjectReferenceKeyframe { time = 0, value = originalValue as Object } });
        }

#if MA_VRCSDK3_AVATARS
        private void EmitNaNimationAction(NaNimationAction action, VirtualClip clip)
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0, action.ShouldDelete ? float.NaN : 1.0f));
            foreach (var bone in action.Bones)
            foreach (var dimension in new[] { "x", "y", "z" })
                clip.SetFloatCurve(EditorCurveBinding.FloatCurve(ObjectPathRemapper.GetVirtualPathForObject(bone), typeof(Transform), $"m_LocalScale.{dimension}"), curve);
            if (action.ShouldDelete && action.TargetProp.TargetObject is SkinnedMeshRenderer smr)
                clip.SetFloatCurve(EditorCurveBinding.FloatCurve(ObjectPathRemapper.GetVirtualPathForObject(smr.gameObject), typeof(SkinnedMeshRenderer), "m_UpdateWhenOffscreen"), AnimationCurve.Constant(0, 1, 0));
        }

        private void ApplyNaNimationAction(NaNimationAction action, bool startsActive)
        {
            var retain = AnimationCurve.Constant(0, 1, 1.0f);
            foreach (var bone in action.Bones)
            {
                var path = ObjectPathRemapper.GetVirtualPathForObject(bone);
                foreach (var dimension in new[] { "x", "y", "z" }) BaseLayerClip.SetFloatCurve(EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalScale.{dimension}"), retain);
                if (!startsActive) continue;
                var constraint = bone.AddComponent<VRCScaleConstraint>();
                constraint.Sources.Add(new VRCConstraintSource { SourceTransform = constraint.transform, Weight = float.NaN });
                constraint.GlobalWeight = float.NaN;
                constraint.Locked = true;
                constraint.IsActive = true;
                BaseLayerClip.SetFloatCurve(EditorCurveBinding.FloatCurve(path, typeof(VRCScaleConstraint), "IsActive"), AnimationCurve.Constant(0, 1, 0));
                BaseLayerClip.SetFloatCurve(EditorCurveBinding.FloatCurve(path, typeof(VRCScaleConstraint), "GlobalWeight"), AnimationCurve.Constant(0, 1, 0));
            }
        }
#endif
    }
}
