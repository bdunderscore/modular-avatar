#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal sealed class StaticMeshRemoval : IDisposable
    {
        private readonly SkinnedMeshRenderer _renderer;
        private readonly List<IMeshSelector> _selectors = new();
        private bool _disposed;

        public StaticMeshRemoval(SkinnedMeshRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Add(IMeshSelector selector)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(StaticMeshRemoval));
            _selectors.Add(selector ?? throw new ArgumentNullException(nameof(selector)));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_renderer == null || _renderer.sharedMesh == null || _selectors.Count == 0) return;

            _renderer.sharedMesh = RemoveVerticesFromMesh.RemoveVertices(
                _renderer,
                _renderer.sharedMesh,
                _selectors);
        }
    }
}
