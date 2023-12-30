#if MA_VRM0 || MA_VRM1

using System;
using System.Collections.Generic;
using UnityEngine;

#if MA_VRM0
using VRM;
#endif

#if MA_VRM1
using UniGLTF.Extensions.VRMC_vrm;
#endif

namespace nadena.dev.modular_avatar.core.vrm
{
    [AddComponentMenu("Modular Avatar/MA Merge VRM0+1 FirstPerson")]
    [DisallowMultipleComponent]
    public class ModularAvatarMergeVRMFirstPerson : AvatarTagComponent
    {
        public List<RendererFirstPersonFlags> renderers = new List<RendererFirstPersonFlags>();
        
        [Serializable]
        public struct RendererFirstPersonFlags
        {
            public Renderer renderer;
            public ModularAvatarFirstPersonFlag firstPersonFlag;

#if MA_VRM0
            public FirstPersonFlag VRM0FirstPersonFlag
            {
                get
                {
                    switch (firstPersonFlag)
                    {
                        case ModularAvatarFirstPersonFlag.Auto: return FirstPersonFlag.Auto;
                        case ModularAvatarFirstPersonFlag.Both: return FirstPersonFlag.Both;
                        case ModularAvatarFirstPersonFlag.ThirdPersonOnly: return FirstPersonFlag.ThirdPersonOnly;
                        case ModularAvatarFirstPersonFlag.FirstPersonOnly: return FirstPersonFlag.FirstPersonOnly;
                        default: throw new ArgumentOutOfRangeException();
                    }
                }
            }
#endif

#if MA_VRM1
            public FirstPersonType VRM1FirstPersonType
            {
                get
                {
                    switch (firstPersonFlag)
                    {
                        case ModularAvatarFirstPersonFlag.Auto: return FirstPersonType.auto;
                        case ModularAvatarFirstPersonFlag.Both: return FirstPersonType.both;
                        case ModularAvatarFirstPersonFlag.ThirdPersonOnly: return FirstPersonType.thirdPersonOnly;
                        case ModularAvatarFirstPersonFlag.FirstPersonOnly: return FirstPersonType.firstPersonOnly;
                        default: throw new ArgumentOutOfRangeException();
                    }
                }
            }
#endif
        }

        public enum ModularAvatarFirstPersonFlag
        {
            Auto,
            Both,
            ThirdPersonOnly,
            FirstPersonOnly,
        }
    }
}

#endif