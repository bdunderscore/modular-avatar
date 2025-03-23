using System.Collections.Generic;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class VRChatSettingsPass : Pass<VRChatSettingsPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var settings = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarVRChatSettings>(true);

            if (settings.Length > 1)
            {
                var objects = new List<object>();
                objects.Add("MA VRChat Settings");
                objects.AddRange(settings);

                BuildReport.LogFatal("error.singleton", objects);
            }
        }
    }
}