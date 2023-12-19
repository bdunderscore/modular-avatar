using System;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.editor.ErrorReporting;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ModularAvatarContext : IExtensionContext
    {
        private IDisposable toDispose;
        internal BuildContext BuildContext { get; private set; }

        public void OnActivate(ndmf.BuildContext context)
        {
            if (BuildContext == null)
            {
                BuildContext = new BuildContext(context);
            }
        }

        public void OnDeactivate(ndmf.BuildContext context)
        {
            toDispose?.Dispose();
            toDispose = null;
        }
    }
}