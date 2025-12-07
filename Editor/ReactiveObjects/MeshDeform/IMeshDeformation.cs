using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    internal interface IMeshDeformation
    {
        public void ProcessPoint(ref Vector3 pos, ref Vector3 norm, ref Vector3 tangent);
        public void RenderGizmo();
    }
}