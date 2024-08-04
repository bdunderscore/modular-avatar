namespace nadena.dev.modular_avatar.core.editor
{
    internal struct ControlCondition
    {
        public string Parameter, DebugName;
        public bool IsConstant;
        public float ParameterValueLo, ParameterValueHi, InitialValue;
        public bool InitiallyActive => InitialValue > ParameterValueLo && InitialValue < ParameterValueHi;
    }
}