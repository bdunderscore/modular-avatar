using nadena.dev.modular_avatar.core.vertex_filters;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(VertexSelectionMode))]
    internal class VertexSelectionModeDrawer : EnumDrawer<VertexSelectionMode>
    {
        protected override string localizationPrefix => "reactive_object.delete-mesh.selection-mode";
    }
}