using System;
using JetBrains.Annotations;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    [AttributeUsage(AttributeTargets.Class)]
    [PublicAPI]
    public class MeshDeformationProvider : Attribute
    {
        public Type ComponentType { get; }

        public MeshDeformationProvider(Type componentType)
        {
            ComponentType = componentType;
        }
    }
}