﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace nadena.dev.modular_avatar.core
{
    public struct ParameterInitialValue
    {
        public bool HasExplicitDefaultSet;
        public float Value;
    }
    
    [Serializable]
    public struct ParameterConfig
    {
        internal const float VALUE_EPSILON = 0.000001f;
        
        public string nameOrPrefix;
        public string remapTo;
        public bool internalParameter, isPrefix;
        public ParameterSyncType syncType;
        public bool localOnly;

        public float defaultValue;
        public bool saved;

        public bool hasExplicitDefaultValue;

        public bool HasDefaultValue => hasExplicitDefaultValue || Mathf.Abs(defaultValue) > VALUE_EPSILON;
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
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/parameters?lang=auto")]
    public class ModularAvatarParameters : AvatarTagComponent
    {
        public List<ParameterConfig> parameters = new List<ParameterConfig>();

        public override void ResolveReferences()
        {
            // no-op
        }
    }
}