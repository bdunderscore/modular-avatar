using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core
{
    internal interface IHaveObjReferences
    {
        IEnumerable<AvatarObjectReference> GetObjectReferences();
    }
}