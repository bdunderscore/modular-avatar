using System.Collections.Generic;
using UnityEditor;

namespace nadena.dev.modular_avatar.animation
{
    internal class EditorCurveBindingComparer : IEqualityComparer<EditorCurveBinding>
    {
        public bool Equals(UnityEditor.EditorCurveBinding x, UnityEditor.EditorCurveBinding y)
        {
            return x.path == y.path && x.type == y.type && x.propertyName == y.propertyName;
        }

        public int GetHashCode(UnityEditor.EditorCurveBinding obj)
        {
            return obj.path.GetHashCode() ^ obj.type.GetHashCode() ^ obj.propertyName.GetHashCode();
        }
    }
}