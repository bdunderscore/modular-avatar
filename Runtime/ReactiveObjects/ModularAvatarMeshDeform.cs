using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    public class ModularAvatarMeshDeform : AvatarTagComponent
    {
        public AvatarObjectReference Target = new();
        public AnimationCurve Falloff;
        public Vector3 Direction;
        public float Strength;
    }
}