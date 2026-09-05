#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class MeshFilterUtilities
    {
        internal static IMeshSelector AggregateVertexFilters(IEnumerable<IMeshSelector?> filters)
        {
            var list = filters.ToList();
            var filter = list.LastOrDefault(f => f != null);
            if (filter is VertexFilterByShape filterByShape)
            {
                return new VertexFilterByShape(filterByShape.Shapes, list
                    .OfType<VertexFilterByShape>()
                    .Min(x => x.Threshold));
            }

            return filter ?? throw new InvalidOperationException("Expected at least one vertex filter to aggregate");
        }
    }
}