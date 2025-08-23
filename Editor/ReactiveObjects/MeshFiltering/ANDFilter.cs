﻿using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class ANDFilter : IVertexFilter
    {
        private readonly List<IVertexFilter> filters;

        public ANDFilter(IEnumerable<IVertexFilter> filters)
        {
            this.filters = (filters ?? throw new ArgumentNullException(nameof(filters))).ToList();
        }

        public bool Equals(IVertexFilter other)
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

        public void MarkFilteredVertices(Transform referenceSpace, Mesh mesh, bool[] filtered)
        {
            foreach (var filter in filters)
            {
                filter.MarkFilteredVertices(referenceSpace, mesh, filtered);
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
            return $"ANDFilter({string.Join(", ", filters.Select(f => f.ToString()))})";
        }
    }
}