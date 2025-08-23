using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IVertexFilterProvider
    {
        IVertexFilter GetFilterFor(IVertexFilterBehavior behavior, ComputeContext context);
    }
}