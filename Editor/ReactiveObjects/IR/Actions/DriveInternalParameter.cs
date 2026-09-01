#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    public class DriveInternalParameter : IAction
    {
        public object TargetKey => new InternalParameterTarget(ParameterName);

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

        public void SetBaseState(BakeContext context, bool actionStartsActive)
        {
            // Note: We explicitly _don't_ set this. This method is intended to set the unity-level base state,
            // but the animator base state for internal parameters will be set via AssignInitialStates, and we don't
            // want to be overwriting that.
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
