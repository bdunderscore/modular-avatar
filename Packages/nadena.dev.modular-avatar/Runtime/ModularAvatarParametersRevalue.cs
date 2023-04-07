using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct ParameterMappingEntry
    {
        public string ParameterName;
        public float OriginalValue;
        public float NewValue;
    }
    
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Parameter Revalue")]
    public class ModularAvatarParametersRevalue : AvatarTagComponent
    {
        public List<ParameterMappingEntry> parameters = new List<ParameterMappingEntry>();
    }
}