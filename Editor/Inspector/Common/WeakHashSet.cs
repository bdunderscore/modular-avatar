using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class WeakHashSet<T> : IEnumerable<T> where T : class
    {
        private Dictionary<int, List<WeakReference<T>>> _refs = new Dictionary<int, List<WeakReference<T>>>();

        private int _amortCounter = 16;

        public void Add(T t)
        {
            WeakReference<T> w = new WeakReference<T>(t);
            int hash = RuntimeHelpers.GetHashCode(t);

            if (!_refs.TryGetValue(hash, out var list))
            {
                list = new List<WeakReference<T>>();
                _refs[hash] = list;
            }

            if (!list.Contains(w))
            {
                list.Add(w);
                if (_amortCounter-- <= 0)
                {
                    ClearDeadReferences();
                }
            }
        }

        private void ClearDeadReferences()
        {
            throw new NotImplementedException();
        }

        public void Remove(T t)
        {
            WeakReference<T> w = new WeakReference<T>(t);
            int hash = RuntimeHelpers.GetHashCode(t);

            if (_refs.TryGetValue(hash, out var list))
            {
                list.RemoveAll(elem => elem.TryGetTarget(out var target) && target == t);
            }
        }

        public bool Contains(T t)
        {
            WeakReference<T> w = new WeakReference<T>(t);
            int hash = RuntimeHelpers.GetHashCode(t);

            if (_refs.TryGetValue(hash, out var list))
            {
                return list.Exists(elem => elem.TryGetTarget(out var target) && target == t);
            }

            return false;
        }


        public IEnumerator<T> GetEnumerator()
        {
            foreach (var list in _refs.Values)
            {
                foreach (var elem in list)
                {
                    if (elem.TryGetTarget(out var target))
                    {
                        yield return target;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}