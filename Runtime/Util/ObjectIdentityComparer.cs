#region

using System.Collections.Generic;
using System.Runtime.CompilerServices;

#endregion

namespace nadena.dev.modular_avatar
{
    internal class ObjectIdentityComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T x, T y)
        {
            return (object)x == (object)y;
        }

        public int GetHashCode(T obj)
        {
            if (obj == null) return 0;
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}