using System;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ZeroIndentScope : IDisposable
    {
        private int oldIndentLevel;

        public ZeroIndentScope()
        {
            oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel = oldIndentLevel;
        }
    }
}