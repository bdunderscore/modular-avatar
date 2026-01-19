#if MA_VRCSDK3_AVATARS

using System.Collections.Immutable;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class ParameterIntrospectionCache
    {
        internal static PropCache<GameObject, ImmutableList<ProvidedParameter>> ProvidedParameterCache =
            new("GetParametersForObject", GetParametersForObject_miss);

        internal static PropCache<GameObject, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>>
            ParameterRemappingCache = new("GetParameterRemappingsAt", GetParameterRemappingsAt_miss);

        private static ImmutableList<ProvidedParameter> GetParametersForObject_miss(ComputeContext ctx, GameObject obj)
        {
            if (obj == null) return ImmutableList<ProvidedParameter>.Empty;

            return ParameterInfo.ForPreview(ctx).GetParametersForObject(obj).ToImmutableList();
        }

        private static ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>
            GetParameterRemappingsAt_miss(ComputeContext ctx, GameObject obj)
        {
            if (obj == null) return ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>.Empty;

            return ParameterInfo.ForPreview(ctx).GetParameterRemappingsAt(obj);
        }

        internal static ImmutableList<ProvidedParameter> GetParametersForObject(GameObject avatar)
        {
            return ProvidedParameterCache.Get(ComputeContext.NullContext, avatar);
        }

        internal static ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> GetParameterRemappingsAt(
            GameObject avatar)
        {
            return ParameterRemappingCache.Get(ComputeContext.NullContext, avatar);
        }

        internal static ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> GetParameterRemappingsAt(
            ComputeContext context,
            GameObject avatar
        )
        {
            return ParameterRemappingCache.Get(context, avatar);
        }
    }
}
#endif