#if MA_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class SiblingCache
    {
        private static readonly PropCache<GameObject, SiblingCache> _cache = new("SiblingCache", Compute);

        public static SiblingCache Get(GameObject avatarRoot)
        {
            return _cache.Get(ComputeContext.NullContext, avatarRoot);
        }

        public ImmutableDictionary<string, ImmutableHashSet<ModularAvatarMenuItem>> ParameterToItems
        {
            get;
            private set;
        }

        private SiblingCache(
            ImmutableDictionary<string, ImmutableHashSet<ModularAvatarMenuItem>> parameterToItems
        )
        {
            ParameterToItems = parameterToItems;
        }

        private static SiblingCache Compute(ComputeContext context, GameObject obj)
        {
            var menuItems = context.GetComponentsInChildren<ModularAvatarMenuItem>(obj, true);

            Dictionary<string, ImmutableHashSet<ModularAvatarMenuItem>.Builder> siblings = new();
            foreach (var item in menuItems)
            {
                var paramName = item.PortableControl?.Parameter;
                if (string.IsNullOrEmpty(paramName)) continue;

                var mappings = ParameterIntrospectionCache.GetParameterRemappingsAt(context, item.gameObject);
                if (mappings.TryGetValue((ParameterNamespace.Animator, paramName),
                        out var replacement))
                    paramName = replacement.ParameterName;

                if (!siblings.TryGetValue(paramName, out var builder))
                {
                    builder = ImmutableHashSet.CreateBuilder<ModularAvatarMenuItem>();
                    siblings[paramName] = builder;
                }

                builder.Add(item);
            }

            return new SiblingCache(
                siblings.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutable())
            );
        }
    }
}

#endif