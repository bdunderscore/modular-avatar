using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ControlCondition
    {
        public string Parameter;
        public Object DebugReference;
        
        public string DebugName;
        public bool IsConstant;
        public float ParameterValueLo, ParameterValueHi, InitialValue;
        public bool InitiallyActive => InitialValue > ParameterValueLo && InitialValue < ParameterValueHi;
        public bool IsConstantActive => InitiallyActive && IsConstant;

        public GameObject ReferenceObject;

        protected bool Equals(ControlCondition other)
        {
            return Parameter == other.Parameter
                   && Equals(DebugReference, other.DebugReference)
                   && DebugName == other.DebugName
                   && IsConstant == other.IsConstant
                   && ParameterValueLo.Equals(other.ParameterValueLo)
                   && ParameterValueHi.Equals(other.ParameterValueHi)
                   && InitialValue.Equals(other.InitialValue)
                   && Equals(ReferenceObject, other.ReferenceObject);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ControlCondition)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Parameter, DebugReference, DebugName, IsConstant, ParameterValueLo,
                ParameterValueHi, InitialValue, ReferenceObject);
        }
    }
}