#nullable enable

namespace nadena.dev.modular_avatar.core
{
    public class ModularAvatarToroidalDeform : AbstractMeshDeformComponent
    {
        public float radius;
        public float aspectRatio;
        public float falloffStartAngle = 30;
        public float falloffEndAngle = 45;
        public bool disableBackHalf;
    }
}