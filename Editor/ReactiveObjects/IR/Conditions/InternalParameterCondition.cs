namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public class InternalParameterCondition : IExpression
    {
        public string ParameterName { get; set; }

        public InternalParameterCondition(string parameterName)
        {
            ParameterName = parameterName;
        }

        public void Walk(ExpressionVisitor visitor)
        {
            // leaf node
        }

        public override string ToString()
        {
            return $"InternalParameterCondition({ParameterName})";
        }

        protected bool Equals(InternalParameterCondition other)
        {
            return ParameterName == other.ParameterName;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InternalParameterCondition)obj);
        }

        public override int GetHashCode()
        {
            return ParameterName != null ? ParameterName.GetHashCode() : 0;
        }
    }
}