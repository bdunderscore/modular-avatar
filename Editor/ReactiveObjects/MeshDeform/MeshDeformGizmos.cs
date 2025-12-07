using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    internal static class MeshDeformGizmos
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            ModularAvatarMeshDeform.OnGizmosCallback = OnDrawGizmosSelected;
        }

        private static void OnDrawGizmosSelected(ModularAvatarMeshDeform meshDeform)
        {
            if (meshDeform.CachedGizmoHandle == null)
            {
                meshDeform.CachedGizmoHandle = new ToroidalDeformation(meshDeform);
            }

            ((IMeshDeformation)meshDeform.CachedGizmoHandle).RenderGizmo();
        }
    }
}