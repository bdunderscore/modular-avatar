using System;
using JetBrains.Annotations;

namespace nadena.dev.modular_avatar.core.editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature, ImplicitUseTargetFlags.Itself)]
    internal class ProvidesVertexFilter : Attribute
    {
        private Type target;

        public ProvidesVertexFilter(Type target)
        {
            this.target = target;
        }

        public Type Target
        {
            get => target;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "Target type cannot be null.");
                target = value;
            }
        }
    }
}