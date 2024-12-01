using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Remove Vertex Color")]
    [DisallowMultipleComponent]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/remove-vertex-color?lang=auto")]
    [PublicAPI]
    public class ModularAvatarRemoveVertexColor : AvatarTagComponent
    {
        [Serializable]
        [PublicAPI]
        public enum RemoveMode
        {
            Remove,
            DontRemove
        }

        public RemoveMode Mode = RemoveMode.Remove;
    }
}