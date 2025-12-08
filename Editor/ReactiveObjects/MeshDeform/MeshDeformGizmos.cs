using nadena.dev.ndmf.preview;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    internal static class MeshDeformGizmos
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            AbstractMeshDeformComponent.OnGizmosCallback = OnDrawGizmosSelected;
        }

        private static void OnDrawGizmosSelected(AbstractMeshDeformComponent deform)
        {
            if (deform.CachedGizmoHandle == null)
            {
                deform.CachedGizmoHandle =
                    MeshDeformDatabase.GetDeformer(ComputeContext.NullContext, deform);
            }

            ((IMeshDeformation)deform.CachedGizmoHandle)?.RenderGizmo();
        }
    }
}