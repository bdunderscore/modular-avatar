#nullable enable

using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByAxisComponent))]
    internal sealed class VertexFilterByAxis : IMeshSelector
    {
        private readonly Vector3 _center;
        private readonly Vector3 _axis;
        private readonly VertexSelectionMode _selectionMode;

        public VertexFilterByAxis(VertexFilterByAxisComponent component, ComputeContext context)
        {
            (_center, _axis, _selectionMode) = context.Observe(
                component,
                c => (c.Center, c.Axis, c.SelectionMode)
            );
        }

        public bool Equals(IMeshSelector other)
        {
            return other is VertexFilterByAxis other2
                   && other2._axis == _axis
                   && other2._center == _center
                   && other2._selectionMode == _selectionMode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(typeof(VertexFilterByAxis), _axis, _center, _selectionMode);
        }

        public override string ToString()
        {
            return $"VertexFilterByAxis: {_axis} @ {_center} ({_selectionMode})";
        }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            return job.MarkPrimitivesFromPositionFilter<AxisFilter>(
                this,
                new AxisFilter { Axis = _axis, Center = _center },
                _selectionMode,
                submesh,
                selectedPrimitives
            );
        }

        [BurstCompile]
        private struct AxisFilter : MeshSelectorJob.IPositionFilter
        {
            public float3 Axis;
            public float3 Center;

            public bool IsVertexSelected(float3 vertexPosition)
            {
                return math.dot(Axis, vertexPosition - Center) > 0f;
            }
        }
    }
}
