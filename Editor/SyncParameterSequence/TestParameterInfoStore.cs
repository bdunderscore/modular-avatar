#if MA_VRCSDK3_AVATARS

#nullable enable

using System.Collections.Generic;

namespace nadena.dev.modular_avatar.core.editor.SyncParameterSequence
{
    internal sealed class TestParameterInfoStore : AbstractParameterInfoStore
    {
        private readonly Dictionary<string, string> _store = new();

        protected override void Store(string blueprintId, string serialized)
        {
            _store[blueprintId] = serialized;
        }

        protected override string? Load(string blueprintId)
        {
            return _store.GetValueOrDefault(blueprintId);
        }

        // Helper for tests to directly inspect the raw serialized store
        public IReadOnlyDictionary<string, string> RawStore => _store;
    }
}

#endif