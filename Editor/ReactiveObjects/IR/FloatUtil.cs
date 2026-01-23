using System;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class FloatUtil
    {
        public static float NextLargest(this float f)
        {
            var bytes = BitConverter.GetBytes(f);
            var intRepresentation = BitConverter.ToInt32(bytes, 0);
            if (f >= 0)
            {
                intRepresentation += 1;
            }
            else
            {
                intRepresentation -= 1;
            }

            var nextBytes = BitConverter.GetBytes(intRepresentation);
            return BitConverter.ToSingle(nextBytes, 0);
        }

        public static float NextSmallest(this float f)
        {
            return -(-f).NextLargest();
        }
    }
}