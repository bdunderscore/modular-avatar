#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core
{
    [Serializable]
    public struct ParameterConfig
    {
        internal const float VALUE_EPSILON = 0.000001f;
        internal const int MIN_BIT_WIDTH = 2;
        internal const int MAX_BIT_WIDTH = 8;
        internal const int DEFAULT_BIT_WIDTH = 8;
        internal const float MIN_LERP_DURATION = 0f;
        internal const float MAX_LERP_DURATION = 300f;

        public string nameOrPrefix;
        public string remapTo;
        public bool internalParameter, isPrefix;
        public ParameterSyncType syncType;
        public bool localOnly;

        public int bitWidth;
        public ParameterRepresentation representation;
        public bool useCustomLerpDuration;
        public float customLerpDuration;

        public float defaultValue;
        public bool saved;

        public bool hasExplicitDefaultValue;

        /// <summary>
        /// Indicates that the default value for this parameter should be applied to any animators attached to the
        /// avatar as well, rather than just the expressions menu configuration.
        ///
        /// Note: Private API for now; will be exposed in 1.10. This is always considered to be true if the parameter
        /// is unsynced and has a default value override.
        /// </summary>
        [SerializeField]
        internal bool m_overrideAnimatorDefaults;

        internal bool OverrideAnimatorDefaults
        {
            get => m_overrideAnimatorDefaults || syncType == ParameterSyncType.NotSynced && HasDefaultValue;
            set => m_overrideAnimatorDefaults = value;
        }

        public bool HasDefaultValue => hasExplicitDefaultValue || Mathf.Abs(defaultValue) > VALUE_EPSILON;

        internal static int ResolveBitWidth(int bitWidth)
        {
            return bitWidth == 0 ? DEFAULT_BIT_WIDTH : Mathf.Clamp(bitWidth, MIN_BIT_WIDTH, MAX_BIT_WIDTH);
        }

        internal static ParameterRepresentation ResolveRepresentation(ParameterRepresentation representation,
            ParameterSyncType syncType)
        {
            if (representation != ParameterRepresentation.Default) return representation;

            return syncType == ParameterSyncType.Float
                ? ParameterRepresentation.Signed
                : ParameterRepresentation.Unsigned;
        }
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

    public enum ParameterRepresentation
    {
        Default,
        Unsigned,
        Signed,
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