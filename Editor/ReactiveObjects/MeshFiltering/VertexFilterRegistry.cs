using System;
using System.Collections.Immutable;
using System.Reflection;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class VertexFilterRegistry
    {
        private static readonly ImmutableDictionary<Type, IVertexFilterProvider> _providers = FindProviders();

        public static bool TryGetVertexFilter(IVertexFilterBehavior behavior, ComputeContext context,
            out IVertexFilter filter)
        {
            filter = default;

            if (!_providers.TryGetValue(behavior.GetType(), out var provider))
            {
                return false;
            }

            filter = provider.GetFilterFor(behavior, context);

            return true;
        }

        private static ImmutableDictionary<Type, IVertexFilterProvider> FindProviders()
        {
            var builder = ImmutableDictionary.CreateBuilder<Type, IVertexFilterProvider>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = (ProvidesVertexFilter)type.GetCustomAttribute(typeof(ProvidesVertexFilter));
                    if (attr == null) continue;

                    if (!typeof(IVertexFilter).IsAssignableFrom(type))
                    {
                        Debug.LogError(
                            $"[ModularAvatar] Vertex filter provider {type.FullName} does not implement IVertexFilter.");
                        continue;
                    }

                    var ctor = type.GetConstructor(new[] { attr.Target, typeof(ComputeContext) });
                    if (ctor == null)
                    {
                        Debug.LogError(
                            $"[ModularAvatar] Vertex filter provider {type.FullName} does not have a constructor that takes ({attr.Target}, ComputeContext).");
                        continue;
                    }

                    builder.Add(attr.Target, new ReflectiveProvider(ctor));
                }
            }

            return builder.ToImmutable();
        }
    }

    internal class ReflectiveProvider : IVertexFilterProvider
    {
        private readonly ConstructorInfo _ctor;

        public ReflectiveProvider(ConstructorInfo ctor)
        {
            _ctor = ctor;
        }

        public IVertexFilter GetFilterFor(IVertexFilterBehavior behavior, ComputeContext context)
        {
            return (IVertexFilter)_ctor.Invoke(new object[] { behavior, context });
        }
    }
}