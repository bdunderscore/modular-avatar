#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

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
                case FloatPropAction prop: prop.Emit(ObjectPathRemapper, clip); break;
                case ObjectPropAction prop: prop.Emit(ObjectPathRemapper, clip); break;
                case NullAction: break;
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
                case FloatPropAction prop: ApplyFloatPropAction(prop, actionStartsActive); break;
                case ObjectPropAction prop: ApplyObjectPropAction(prop, actionStartsActive); break;
                case NullAction: break;
                default:
                    Debug.LogWarning($"Unsupported action type: {action.GetType().FullName}");
                    break;
            }
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


        private void ApplyFloatPropAction(FloatPropAction action, bool startsActive)
        {
            var binding = FloatPropAction.GetCurveBinding(action.Prop, ObjectPathRemapper);
            if (!binding.HasValue) return;

            var targetObject = action.Prop.TargetObject;
            if (targetObject is SkinnedMeshRenderer smr && action.Prop.PropertyName.StartsWith("blendShape."))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) return;

                var index = mesh.GetBlendShapeIndex(action.Prop.PropertyName[11..]);
                if (index < 0) return;

                BaseLayerClip.SetFloatCurve(binding.Value,
                    AnimationCurve.Constant(0, 1, smr.GetBlendShapeWeight(index)));
                if (startsActive && !float.IsNaN(action.Value))
                    smr.SetBlendShapeWeight(index, action.Value);
                return;
            }

            if (targetObject == null) return;

            var serializedObject = new SerializedObject(targetObject);
            var property = serializedObject.FindProperty(action.Prop.PropertyName);
            if (property == null) return;

            var changed = false;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    BaseLayerClip.SetFloatCurve(binding.Value,
                        AnimationCurve.Constant(0, 1, property.boolValue ? 1 : 0));
                    if (startsActive)
                    {
                        property.boolValue = action.Value != 0;
                        changed = true;
                    }

                    break;
                case SerializedPropertyType.Float:
                    BaseLayerClip.SetFloatCurve(binding.Value,
                        AnimationCurve.Constant(0, 1, property.floatValue));
                    if (startsActive && !float.IsNaN(action.Value))
                    {
                        property.floatValue = action.Value;
                        changed = true;
                    }

                    break;
                default:
                    return;
            }

            if (changed) serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ApplyObjectPropAction(ObjectPropAction action, bool startsActive)
        {
            var binding = ObjectPropAction.GetCurveBinding(action.Prop, ObjectPathRemapper);
            if (!binding.HasValue || action.Prop.TargetObject == null) return;

            var serializedObject = new SerializedObject(action.Prop.TargetObject);
            var property = serializedObject.FindProperty(action.Prop.PropertyName);
            if (property is not { propertyType: SerializedPropertyType.ObjectReference }) return;

            BaseLayerClip.SetObjectCurve(binding.Value,
                new[] { new ObjectReferenceKeyframe { time = 0, value = property.objectReferenceValue } });
            if (!startsActive) return;

            property.objectReferenceValue = action.Value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
