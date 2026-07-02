using UnityEngine;

namespace nadena.dev.modular_avatar.ui
{
    internal class CurveAttribute : PropertyAttribute
    {
        public float PosX { get; }
        public float PosY { get; }
        public float RangeX { get; }
        public float RangeY { get; }

        public CurveAttribute(float posX, float posY, float rangeX, float rangeY)
        {
            PosX = posX;
            PosY = posY;
            RangeX = rangeX;
            RangeY = rangeY;
        }
    }
}
