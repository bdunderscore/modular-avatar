#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal readonly struct ParameterTarget
    {
        public string ParameterName { get; }

        public ParameterTarget(string parameterName)
        {
            ParameterName = parameterName;
        }

        public override string ToString()
        {
            return ParameterName;
        }

        private bool Equals(ParameterTarget other)
        {
            return ParameterName == other.ParameterName;
        }

        public override bool Equals(object? obj)
        {
            return obj is ParameterTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ParameterName != null ? ParameterName.GetHashCode() : 0;
        }
    }
}
