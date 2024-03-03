#if MA_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.ndmf;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class PruneParametersPass : Pass<PruneParametersPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            if (!context.AvatarDescriptor) return;

            var expParams = context.AvatarDescriptor.expressionParameters;
            if (expParams != null && context.IsTemporaryAsset(expParams))
            {
                expParams.parameters = expParams.parameters.Where(p => !string.IsNullOrEmpty(p.name)).ToArray();
            }
        }
    }
}

#endif