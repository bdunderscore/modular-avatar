using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;

namespace nadena.dev.modular_avatar.core.editor
{
    internal interface IMeshSelectorProvider
    {
        IMeshSelector GetSelectorFor(IMeshSelectorBehavior behavior, ComputeContext context);
    }
}