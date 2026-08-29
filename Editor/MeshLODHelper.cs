using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class MeshLODHelper
    {
        public static (int, int) GetSubmeshIndexRange(this Mesh mesh, int submeshIndex)
        {
            var submesh = mesh.GetSubMesh(submeshIndex);
            int start = (int) submesh.indexStart;
            int len = (int) submesh.indexCount;

            #if UNITY_6000_2_OR_NEWER
            var lod = mesh.GetLod(submeshIndex, 0);
            start = (int)lod.indexStart + mesh.GetSubMesh(submeshIndex).indexStart;
            len = (int)lod.indexCount;
            #endif

            return (start, len);
        }
    }
}
