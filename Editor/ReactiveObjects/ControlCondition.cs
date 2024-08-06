﻿using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ControlCondition
    {
        public string Parameter, DebugName;
        public bool IsConstant;
        public float ParameterValueLo, ParameterValueHi, InitialValue;
        public bool InitiallyActive => InitialValue > ParameterValueLo && InitialValue < ParameterValueHi;
        public bool IsConstantActive => InitiallyActive && IsConstant;

        public GameObject ReferenceObject;
    }
}