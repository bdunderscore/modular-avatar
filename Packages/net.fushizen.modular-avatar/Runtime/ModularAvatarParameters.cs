using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.modular_avatar.core
{
    [Serializable]
    public struct ParameterConfig
    {
        public string nameOrPrefix;
        public string remapTo;
        public bool internalParameter, isPrefix;
        public ParameterSyncType syncType;
        public float defaultValue;
        public bool saved;
    }

    public enum ParameterSyncType
    {
        NotSynced,
        Int,
        Float,
        Bool,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Parameters")]
    public class ModularAvatarParameters : AvatarTagComponent
    {
        public List<ParameterConfig> parameters = new List<ParameterConfig>();
    }
}