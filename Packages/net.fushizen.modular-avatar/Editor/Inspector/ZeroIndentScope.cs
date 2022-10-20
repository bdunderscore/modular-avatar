using System;
using UnityEditor;

namespace net.fushizen.modular_avatar.core.editor
{
    public class ZeroIndentScope : IDisposable
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