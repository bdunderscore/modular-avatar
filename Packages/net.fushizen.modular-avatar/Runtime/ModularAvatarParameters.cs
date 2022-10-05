using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.fushizen.modular_avatar.core
{
    [Serializable]
    public struct ParameterConfig
    {
        public string nameOrPrefix;
        public string remapTo;
        public bool internalParameter, isPrefix;
    }

    public class ModularAvatarParameters : AvatarTagComponent
    {
        public List<ParameterConfig> parameters = new List<ParameterConfig>();
    }
}