#nullable enable

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    public readonly struct InternalParameterTarget
    {
        public string ParameterName { get; }

        public InternalParameterTarget(string parameterName)
        {
            ParameterName = parameterName;
        }

        public override string ToString()
        {
            return ParameterName;
        }

        private bool Equals(InternalParameterTarget other)
        {
            return ParameterName == other.ParameterName;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is InternalParameterTarget other && Equals(other));
        }

        public override int GetHashCode()
        {
            return ParameterName != null ? ParameterName.GetHashCode() : 0;
        }
    }
}
