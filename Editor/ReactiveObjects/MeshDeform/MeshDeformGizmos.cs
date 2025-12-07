using nadena.dev.ndmf.preview;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor.MeshDeform
{
    internal static class MeshDeformGizmos
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            ModularAvatarToroidalDeform.OnGizmosCallback = OnDrawGizmosSelected;
        }

        private static void OnDrawGizmosSelected(ModularAvatarToroidalDeform toroidalDeform)
        {
            if (toroidalDeform.CachedGizmoHandle == null)
            {
                toroidalDeform.CachedGizmoHandle = new ToroidalDeformation(ComputeContext.NullContext, toroidalDeform);
            }

            ((IMeshDeformation)toroidalDeform.CachedGizmoHandle).RenderGizmo();
        }
    }
}