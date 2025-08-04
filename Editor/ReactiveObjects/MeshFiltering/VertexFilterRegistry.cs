using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class VertexFilterRegistry
    {
        private static readonly ImmutableDictionary<Type, IVertexFilterProvider> _providers = FindProviders();
        private static readonly string[] _componentLabels;

        internal static List<string> ComponentLabels => _componentLabels.ToList();
        internal static ImmutableDictionary<string, Type> LabelToType { get; }

        static VertexFilterRegistry()
        {
            _componentLabels = new string[_providers.Count];
            var builder = ImmutableDictionary.CreateBuilder<string, Type>();

            var i = 0;
            foreach (var kvp in _providers.Select(kv => (GetComponentLabel(kv.Key), kv.Key))
                         .OrderBy(p => p.Item1))
            {
                _componentLabels[i] = new string(kvp.Item1);
                builder.Add(kvp.Item1, kvp.Item2);
                i++;
            }

            LabelToType = builder.ToImmutable();

            string GetComponentLabel(Type type)
            {
                var acm = (AddComponentMenu)type.GetCustomAttribute(typeof(AddComponentMenu));
                if (acm == null) return type.Name;

                var lastSlash = acm.componentMenu.LastIndexOf('/');
                if (lastSlash < 0) return acm.componentMenu;
                return acm.componentMenu.Substring(lastSlash + 1);
            }
        }

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