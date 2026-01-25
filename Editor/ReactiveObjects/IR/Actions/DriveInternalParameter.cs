using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public class DriveInternalParameter : IAction
    {
        public object TargetKey => new ParameterTarget(ParameterName);

        public string ParameterName { get; set; }
        public bool State { get; set; }

        public DriveInternalParameter(string parameterName, bool state)
        {
            ParameterName = parameterName;
            State = state;
        }

        public override string ToString()
        {
            return $"DriveInternalParameter({ParameterName}, {State})";
        }

        public void ToMotion(BakeContext context, VirtualClip clip)
        {
            clip.SetFloatCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), ParameterName),
                AnimationCurve.Constant(0, 1, State ? 1 : 0)
            );
        }
    }
}