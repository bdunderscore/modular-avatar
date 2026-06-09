using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using Unity.Collections;
using Unity.Jobs;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class ANDFilter : IMeshSelector
    {
        private readonly List<IMeshSelector> filters;

        public ANDFilter(IEnumerable<IMeshSelector> filters)
        {
            this.filters = (filters ?? throw new ArgumentNullException(nameof(filters))).ToList();
        }

        public bool Equals(IMeshSelector other)
        {
            if (other is ANDFilter andFilter)
            {
                if (filters.Count != andFilter.filters.Count) return false;

                for (var i = 0; i < filters.Count; i++)
                {
                    if (!filters[i].Equals(andFilter.filters[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            var hash = typeof(ANDFilter).GetHashCode();
            foreach (var filter in filters)
            {
                hash = hash * 31 + filter.GetHashCode();
            }

            return hash;
        }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> prims)
        {
            return MeshSelectorCombine.Combine(true, filters, job, submesh, prims);
        }

        public void Observe(ComputeContext context)
        {
            foreach (var filter in filters)
            {
                filter.Observe(context);
            }
        }

        public override string ToString()
        {
            return $"ANDFilter({string.Join(", ", filters.Select(f => f.ToString()))})";
        }
    }
}