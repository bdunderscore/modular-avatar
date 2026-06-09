using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using Unity.Collections;
using Unity.Jobs;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class ORFilter : IMeshSelector
    {
        private readonly List<IMeshSelector> filters;

        public ORFilter(IEnumerable<IMeshSelector> filters)
        {
            this.filters = (filters ?? throw new ArgumentNullException(nameof(filters))).ToList();
        }

        public bool Equals(IMeshSelector other)
        {
            if (other is ORFilter orFilter)
            {
                if (filters.Count != orFilter.filters.Count) return false;

                for (var i = 0; i < filters.Count; i++)
                {
                    if (!filters[i].Equals(orFilter.filters[i]))
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
            return MeshSelectorCombine.Combine(false, filters, job, submesh, prims);
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
            return $"ORFilter({string.Join(", ", filters.Select(f => f.ToString()))})";
        }
    }
}