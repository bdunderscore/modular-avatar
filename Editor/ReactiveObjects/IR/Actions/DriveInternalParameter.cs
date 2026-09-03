#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class DriveInternalParameter : IAction
    {
        public object TargetKey => new InternalParameterTarget(ParameterName);
        public string ParameterName { get; set; }
        public bool State { get; set; }

        public DriveInternalParameter(string parameterName, bool state)
        {
            ParameterName = parameterName;
            State = state;
        }

        public override string ToString() => $"DriveInternalParameter({ParameterName}, {State})";
    }
}
