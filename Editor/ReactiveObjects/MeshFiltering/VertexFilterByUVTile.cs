#nullable enable

using System;
using nadena.dev.modular_avatar.core.vertex_filters;
using nadena.dev.ndmf.preview;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace nadena.dev.modular_avatar.core.editor
{
    [ProvidesVertexFilter(typeof(VertexFilterByUVTileComponent))]
    internal sealed class VertexFilterByUVTile : IMeshSelector
    {
        private readonly int _uvChannel;
        private readonly bool _useUMin, _useUMax, _useVMin, _useVMax;
        private readonly bool _uMinInclusive, _uMaxInclusive, _vMinInclusive, _vMaxInclusive;
        private readonly float _uMin, _uMax, _vMin, _vMax;
        private readonly bool _invert;
        private readonly VertexSelectionMode _selectionMode;

        public VertexFilterByUVTile(VertexFilterByUVTileComponent component, ComputeContext context)
        {
            (_uvChannel, _useUMin, _uMinInclusive, _uMin, _useUMax, _uMaxInclusive, _uMax,
                    _useVMin, _vMinInclusive, _vMin, _useVMax, _vMaxInclusive, _vMax, _invert, _selectionMode)
                = context.Observe(component, c => (
                    c.UVChannel,
                    c.UseUMin, c.UMinInclusive, c.UMin,
                    c.UseUMax, c.UMaxInclusive, c.UMax,
                    c.UseVMin, c.VMinInclusive, c.VMin,
                    c.UseVMax, c.VMaxInclusive, c.VMax,
                    c.Invert, c.SelectionMode
                ));
        }

        public bool Equals(IMeshSelector other)
        {
            return other is VertexFilterByUVTile other2
                   && other2._uvChannel == _uvChannel
                   && other2._useUMin == _useUMin
                   && other2._uMinInclusive == _uMinInclusive
                   && other2._uMin == _uMin
                   && other2._useUMax == _useUMax
                   && other2._uMaxInclusive == _uMaxInclusive
                   && other2._uMax == _uMax
                   && other2._useVMin == _useVMin
                   && other2._vMinInclusive == _vMinInclusive
                   && other2._vMin == _vMin
                   && other2._useVMax == _useVMax
                   && other2._vMaxInclusive == _vMaxInclusive
                   && other2._vMax == _vMax
                   && other2._invert == _invert
                   && other2._selectionMode == _selectionMode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                typeof(VertexFilterByUVTile),
                HashCode.Combine(_uvChannel, _useUMin, _uMinInclusive, _uMin, _useUMax, _uMaxInclusive, _uMax),
                HashCode.Combine(_useVMin, _vMinInclusive, _vMin, _useVMax, _vMaxInclusive, _vMax, _invert,
                    _selectionMode)
            );
        }

        public override string ToString()
        {
            var parts = (_useUMin ? (_uMinInclusive ? "U≥" : "U>") + _uMin : "")
                        + (_useUMax ? (_uMaxInclusive ? "U≤" : "U<") + _uMax : "")
                        + (_useVMin ? (_vMinInclusive ? "V≥" : "V>") + _vMin : "")
                        + (_useVMax ? (_vMaxInclusive ? "V≤" : "V<") + _vMax : "");
            return $"VertexFilterByUVTile: uv{_uvChannel} [{parts}] {(_invert ? "invert " : "")}({_selectionMode})";
        }

        public JobHandle MarkFilteredPrimitives(MeshSelectorJob job, int submesh, NativeSlice<bool> selectedPrimitives)
        {
            return job.MarkPrimitivesFromUVFilter(
                this,
                new UVTileFilter
                {
                    UMin = _useUMin ? _uMin : float.NegativeInfinity,
                    UMax = _useUMax ? _uMax : float.PositiveInfinity,
                    VMin = _useVMin ? _vMin : float.NegativeInfinity,
                    VMax = _useVMax ? _vMax : float.PositiveInfinity,
                    UMinInclusive = _uMinInclusive,
                    UMaxInclusive = _uMaxInclusive,
                    VMinInclusive = _vMinInclusive,
                    VMaxInclusive = _vMaxInclusive,
                    Invert = _invert
                },
                _selectionMode,
                submesh,
                selectedPrimitives,
                _uvChannel
            );
        }

        [BurstCompile]
        private struct UVTileFilter : MeshSelectorJob.IUVFilter
        {
            public float UMin, UMax, VMin, VMax;
            public bool UMinInclusive, UMaxInclusive, VMinInclusive, VMaxInclusive;
            public bool Invert;

            public bool IsVertexSelected(float2 uv)
            {
                // The "inclusive" booleans describe the kept region (the conventional meaning):
                //   strict   (`<`)  → boundary is excluded from the kept region
                //   inclusive(`<=`) → boundary is included   in the kept region
                // UMin is a lower bound (kept: u >= UMin when inclusive, u > UMin when strict).
                // UMax is an upper bound (kept: u <= UMax when inclusive, u < UMax when strict).
                var uMinOk = UMinInclusive ? uv.x >= UMin : uv.x > UMin;
                var uMaxOk = UMaxInclusive ? uv.x <= UMax : uv.x < UMax;
                var vMinOk = VMinInclusive ? uv.y >= VMin : uv.y > VMin;
                var vMaxOk = VMaxInclusive ? uv.y <= VMax : uv.y < VMax;

                var inside = uMinOk && uMaxOk && vMinOk && vMaxOk;
                return Invert ? !inside : inside;
            }
        }
    }
}