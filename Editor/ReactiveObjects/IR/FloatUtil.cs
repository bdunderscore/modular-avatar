#nullable enable

using System;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class FloatUtil
    {
        public static float NextLargest(this float f)
        {
            if (float.IsNaN(f) || f == float.PositiveInfinity) return f;
            if (f == 0) return float.Epsilon;

            var bytes = BitConverter.GetBytes(f);
            var intRepresentation = BitConverter.ToInt32(bytes, 0);
            intRepresentation += f > 0 ? 1 : -1;

            var nextBytes = BitConverter.GetBytes(intRepresentation);
            return BitConverter.ToSingle(nextBytes, 0);
        }

        public static float NextSmallest(this float f)
        {
            return -(-f).NextLargest();
        }
    }
}
