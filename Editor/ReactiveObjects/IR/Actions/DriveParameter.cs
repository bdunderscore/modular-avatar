#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public class DriveParameter : IAction
    {
        public object TargetKey => new ParameterTarget(ParameterName);

        public string ParameterName { get; set; }
        public float Value { get; set; }

        public DriveParameter(string parameterName, float value)
        {
            ParameterName = parameterName;
            Value = value;
        }

        public override string ToString()
        {
            return $"DriveParameter({ParameterName}, {Value})";
        }

        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            if (actionStartsActive)
            {
                context.SetParameter(ParameterName, Value);
            }
            else
            {
                context.EnsureParameterPresent(ParameterName);
            }
        }
        
        public void ToMotion(BakeContext context, VirtualClip clip)
        {
            clip.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), ParameterName),
                AnimationCurve.Constant(0, 1, Value)
            );
        }
    }
}
