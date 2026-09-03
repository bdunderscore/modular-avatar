#nullable enable

using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class DriveParameter : IAction
    {
        public object TargetKey => new ParameterTarget(ParameterName);
        public string ParameterName { get; set; }
        public float Value { get; set; }

        public DriveParameter(string parameterName, float value)
        {
            ParameterName = parameterName;
            Value = value;
        }

        public override string ToString() => $"DriveParameter({ParameterName}, {Value})";
    }
}
