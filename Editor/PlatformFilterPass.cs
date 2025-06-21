using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class PlatformFilterPass : Pass<PlatformFilterPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var filters = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarPlatformFilter>(true);
            var curPlatform = context.PlatformProvider.QualifiedName;

            var byObject = filters.GroupBy(pf => pf.gameObject).ToList();
            
            foreach (var group in byObject)
            {
                var obj = group.Key;
                if (obj == null) continue;

                bool hasIncludes = group.Any(f => !f.ExcludePlatform);
                var hasExcludes = group.Any(f => f.ExcludePlatform);
                if (hasIncludes && hasExcludes)
                {
                    ErrorReport.ReportError(Localization.L, ErrorSeverity.Error,
                        "validation.platform_filter.mixed_include_exclude");
                }

                bool isIncluded = group.Any(f => f.Platform == curPlatform && !f.ExcludePlatform);
                bool isExcluded = group.Any(f => f.Platform == curPlatform && f.ExcludePlatform);

                if (hasIncludes && !isIncluded)
                {
                    Object.DestroyImmediate(obj);
                    continue;
                }

                if (isExcluded)
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        internal void Process(ndmf.BuildContext context)
        {
            Execute(context);
        }
    }
}