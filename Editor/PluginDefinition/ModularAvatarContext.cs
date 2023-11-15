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

            toDispose = BuildReport.CurrentReport.ReportingOnAvatar(context.AvatarRootObject);
        }

        public void OnDeactivate(ndmf.BuildContext context)
        {
            try
            {
                toDispose?.Dispose();
                toDispose = null;

                if (BuildReport.CurrentReport?.CurrentAvatar?.successful == false)
                {
                    // This is a bit of a temporary hack until we have a better way to report errors via NDMF
                    ErrorReportUI.OpenErrorReportUI();
                    throw new Exception("Errors occurred during modular avatar processing");
                }
            }
            catch (Exception e)
            {
                ErrorReportUI.OpenErrorReportUI();
                throw e;
            }
        }
    }
}