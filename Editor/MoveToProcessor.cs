#nullable enable

using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MoveToPluginPass : Pass<MoveToPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            foreach (var moveTo in context.AvatarRootObject.GetComponentsInChildren<ModularAvatarMoveTo>(true))
            {
                BuildReport.ReportingObject(moveTo, () =>
                {
                    moveTo.MatchTarget();
                    Object.DestroyImmediate(moveTo);
                });
            }
        }

        internal void ExecuteForTesting(ndmf.BuildContext context)
        {
            Execute(context);
        }
    }
}