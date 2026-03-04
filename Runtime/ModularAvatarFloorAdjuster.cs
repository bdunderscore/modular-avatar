using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Modular Avatar/MA Floor Adjuster")]
    [HelpURL("https://modular-avatar.nadena.dev/docs/reference/floor-adjuster?lang=auto")]
    public class ModularAvatarFloorAdjuster : AvatarTagComponent
    {
        // No fields

        private readonly Vector3[] _gizmoPoints = new Vector3[4];

        private void OnDrawGizmosSelected()
        {
            var oldColor = Gizmos.color;
            Gizmos.color = Color.red;
            var i = 0;

            for (var dx = -1; dx <= 1; dx += 2)
            {
                for (var dz = -dx; Math.Abs(dz) <= 1; dz += dx * 2)
                {
                    _gizmoPoints[i++] = transform.position + new Vector3(dx, 0, dz) * 0.5f;
                }
            }
            
            Gizmos.DrawLineStrip(new ReadOnlySpan<Vector3>(_gizmoPoints), true);

            Gizmos.color = oldColor;
        }
    }
}