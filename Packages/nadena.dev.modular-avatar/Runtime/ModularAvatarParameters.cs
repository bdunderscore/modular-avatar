﻿using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct ParameterConfig
    {
        public string nameOrPrefix;
        public string remapTo;
        public bool internalParameter, isPrefix;
        public ParameterSyncType syncType;
        public bool localOnly;
        public float defaultValue;
        public bool saved;
    }

    /**
     * This enum is a bit poorly named, having been introduced before local-only parameters were a thing. In actuality,
     * this is the parameter type - NotSynced indicates the parameter should not be registered in Expression Parameters.
     */
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