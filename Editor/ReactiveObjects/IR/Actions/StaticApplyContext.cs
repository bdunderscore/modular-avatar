#nullable enable

using System;
using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal enum StaticApplyResult
    {
        Applied,
        Retain
    }

    internal sealed class StaticApplyContext : IDisposable
    {
        private readonly Dictionary<ResourceKey, IDisposable> _resources = new();
        private readonly List<IDisposable> _creationOrder = new();
        private bool _disposed;

        public T Get<K, T>(K key, Func<K, T> ctor)
            where K : notnull
            where T : IDisposable
        {
            if (_disposed) throw new ObjectDisposedException(nameof(StaticApplyContext));
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (ctor == null) throw new ArgumentNullException(nameof(ctor));

            var resourceKey = new ResourceKey(typeof(T), key);
            if (_resources.TryGetValue(resourceKey, out var existing))
                return (T)existing;

            var resource = ctor(key);
            if (resource is null)
                throw new InvalidOperationException($"{nameof(ctor)} returned null");

            _resources.Add(resourceKey, resource);
            _creationOrder.Add(resource);
            return resource;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            List<Exception>? exceptions = null;
            for (var index = _creationOrder.Count - 1; index >= 0; index--)
            {
                try
                {
                    _creationOrder[index].Dispose();
                }
                catch (Exception exception)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(exception);
                }
            }

            _creationOrder.Clear();
            _resources.Clear();

            if (exceptions != null)
                throw new AggregateException("One or more static application resources failed to dispose", exceptions);
        }

        private readonly struct ResourceKey : IEquatable<ResourceKey>
        {
            private readonly Type _resourceType;
            private readonly object _key;

            public ResourceKey(Type resourceType, object key)
            {
                _resourceType = resourceType;
                _key = key;
            }

            public bool Equals(ResourceKey other)
            {
                return _resourceType == other._resourceType && Equals(_key, other._key);
            }

            public override bool Equals(object? obj)
            {
                return obj is ResourceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_resourceType, _key);
            }
        }
    }
}