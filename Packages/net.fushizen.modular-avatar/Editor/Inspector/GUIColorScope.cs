using System;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    public class GUIColorScope : IDisposable
    {
        private readonly Color _oldColor;

        public GUIColorScope(Color color)
        {
            _oldColor = GUI.color;
            GUI.color = color;
        }

        public void Dispose()
        {
            GUI.color = _oldColor;
        }
    }
}