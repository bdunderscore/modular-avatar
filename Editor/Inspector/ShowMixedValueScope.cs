using System;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ShowMixedValueScope : IDisposable
    {
        private bool _oldShowMixedValue;

        public ShowMixedValueScope(bool showMixedValue)
        {
            _oldShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = showMixedValue;
        }

        public void Dispose()
        {
            EditorGUI.showMixedValue = _oldShowMixedValue;
        }
    }
}
