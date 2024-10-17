using System;
using System.Collections.Generic;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.trace;

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
            public int Generation;
        }

        private readonly string _debugName;
        private readonly Func<ComputeContext, Key, Value> _operator;
        private readonly Func<Value, Value, bool> _equalityComparer;
        private readonly Dictionary<Key, CacheEntry> _cache = new();

        private static int _generation = 0;

        public PropCache(string debugName, Func<ComputeContext, Key, Value> operatorFunc,
            Func<Value, Value, bool> equalityComparer = null)
        {
            _debugName = debugName;
            _operator = operatorFunc;
            _equalityComparer = equalityComparer;
        }
        
        private static void InvalidateEntry(CacheEntry entry)
        {
            var newGenContext = new ComputeContext("PropCache/" + entry.DebugName + " key " + FormatKey(entry.Key) + " gen=" + _generation++);
            var newValue = entry.Owner._operator(newGenContext, entry.Key);
            if (!entry.ObserverContext.IsInvalidated && entry.Owner._equalityComparer != null && entry.Owner._equalityComparer(entry.Value, newValue))
            {
                TraceBuffer.RecordTraceEvent(
                    "PropCache.InvalidateEntry", 
                    (ev) => $"[PropCache/{ev.Arg0}] Value did not change, retaining result (new gen={ev.Arg1})",
                    entry.DebugName, entry.Generation
                );
                
                entry.GenerateContext = newGenContext;
                entry.GenerateContext.InvokeOnInvalidate(entry, InvalidateEntry);
                return;
            }
            
            var trace = TraceBuffer.RecordTraceEvent(
                "PropCache.InvalidateEntry", 
                (ev) => $"[PropCache/{ev.Arg0}] Value changed, invalidating",
                entry.DebugName
            );
            
            entry.Owner._cache.Remove(entry.Key);
            using (trace.Scope()) entry.ObserverContext.Invalidate();
        }
        
        public Value Get(ComputeContext context, Key key)
        {
            TraceEvent ev;
            var formattedKey = FormatKey(key);
            if (!_cache.TryGetValue(key, out var entry) || entry.GenerateContext.IsInvalidated)
            {
                var curGen = _generation++;
                
                var subContext = new ComputeContext("PropCache/" + _debugName + " key " + formattedKey + " gen=" + curGen);
                entry = new CacheEntry
                {
                    GenerateContext = subContext,
                    ObserverContext = new ComputeContext("Observer for PropCache/" + _debugName + " for key " +
                                                         formattedKey + " gen=" + curGen),
                    Owner = this,
                    Key = key,
                    DebugName = _debugName,
                    Generation = curGen
                };

                ev = TraceBuffer.RecordTraceEvent(
                    "PropCache.Get",
                    (ev) =>
                    {
                        var entry_ = (CacheEntry)ev.Arg0;
                        return
                            $"[PropCache/{entry_.DebugName}] Cache miss for key {entry_.Key} gen={entry_.Generation} from context {ev.Arg1}";
                    },
                    entry, context
                );

                _cache[key] = entry;
                using (ev.Scope())
                {
                    entry.Value = _operator(subContext, key);
                    entry.GenerateContext.InvokeOnInvalidate(entry, InvalidateEntry);
                }
            }
            else
            {
                ev = TraceBuffer.RecordTraceEvent(
                    "PropCache.Get", 
                    (ev) =>
                    {
                        var entry_ = (CacheEntry) ev.Arg0;
                        return $"[PropCache/{entry_.DebugName}] Cache hit for key {entry_.Key} gen={entry_.Generation} from context {ev.Arg1}";
                    },
                    entry, context
                );
            }
            
            entry.ObserverContext.Invalidates(context);

            return entry.Value;
        }

        private static string FormatKey(object obj)
        {
            if (obj is UnityEngine.Object unityObj)
            {
                return $"{unityObj.GetHashCode()}#{unityObj}";
            }
            else
            {
                return "" + obj;
            }
        }
    }
}