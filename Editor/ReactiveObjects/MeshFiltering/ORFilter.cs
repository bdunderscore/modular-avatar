using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class ORFilter : IVertexFilter
    {
        private readonly List<IVertexFilter> filters;

        public ORFilter(IEnumerable<IVertexFilter> filters)
        {
            this.filters = (filters ?? throw new ArgumentNullException(nameof(filters))).ToList();
        }

        public bool Equals(IVertexFilter other)
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

        public void MarkFilteredVertices(Renderer renderer, Mesh mesh, bool[] filtered)
        {
            foreach (var filter in filters)
            {
                filter.MarkFilteredVertices(renderer, mesh, filtered);
            }
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