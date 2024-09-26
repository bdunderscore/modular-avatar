using System;
using System.Collections.Generic;
using nadena.dev.ndmf.preview;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class PropCache<Key, Value>
    {
        private class CacheEntry
        {
            public ComputeContext GenerateContext, ObserverContext;
            public PropCache<Key, Value> Owner;
            public Key Key;
            public Value Value;
            public string DebugName;
        }

        private readonly string _debugName;
        private readonly Func<ComputeContext, Key, Value> _operator;
        private readonly Func<Value, Value, bool> _equalityComparer;
        private readonly Dictionary<Key, CacheEntry> _cache = new();

        public PropCache(string debugName, Func<ComputeContext, Key, Value> operatorFunc,
            Func<Value, Value, bool> equalityComparer = null)
        {
            _debugName = debugName;
            _operator = operatorFunc;
            _equalityComparer = equalityComparer;
        }
        
        private static void InvalidateEntry(CacheEntry entry)
        {
            var newGenContext = new ComputeContext("PropCache/" + entry.DebugName + " key " + entry.Key);
            var newValue = entry.Owner._operator(newGenContext, entry.Key);
            if (entry.Owner._equalityComparer != null && entry.Owner._equalityComparer(entry.Value, newValue))
            {
                entry.GenerateContext = newGenContext;
                entry.GenerateContext.InvokeOnInvalidate(entry, InvalidateEntry);
                return;
            }
            
            entry.Owner._cache.Remove(entry.Key);
            entry.ObserverContext.Invalidate();
        }
        
        public Value Get(ComputeContext context, Key key)
        {
            if (!_cache.TryGetValue(key, out var entry) || entry.GenerateContext.IsInvalidated)
            {
                var subContext = new ComputeContext("PropCache/" + _debugName + " key " + key);
                entry = new CacheEntry
                {
                    GenerateContext = subContext,
                    ObserverContext = new ComputeContext("Observer for PropCache for key " + key),
                    Owner = this,
                    Key = key,
                    Value = _operator(subContext, key),
                    DebugName = _debugName
                };
                _cache[key] = entry;
                
                subContext.InvokeOnInvalidate(entry, InvalidateEntry);
            }
            
            entry.ObserverContext.Invalidates(context);

            return entry.Value;
        }
    }
}