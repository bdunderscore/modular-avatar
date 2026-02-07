using System;
using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class DeferRefresh
    {
        private static readonly SortedDictionary<int, Dictionary<object, Action>> _pending = new();
        private static int _suppressedCount;

        public static void Invoke(int priority, object key, Action action)
        {
            if (_suppressedCount == 0 && _pending.Count == 0)
            {
                action();
            }
            else
            {
                if (!_pending.TryGetValue(priority, out var dict))
                {
                    dict = new Dictionary<object, Action>();
                    _pending[priority] = dict;
                }

                dict[key] = action;
            }
        }

        public static IDisposable Suppress()
        {
            _suppressedCount++;

            return new Scope();
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
                _suppressedCount--;

                if (_suppressedCount != 0) return;
                
                while (_pending.Count > 0)
                {
                    var first = _pending.First();
                    var (key, action) = first.Value.First();
                    first.Value.Remove(key);
                    if (first.Value.Count == 0) _pending.Remove(first.Key);

                    action();
                }
            }
        }
    }
}