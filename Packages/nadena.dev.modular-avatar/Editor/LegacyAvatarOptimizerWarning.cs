#if LEGACY_AVATAR_OPTIMIZER
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [InitializeOnLoad]
    public class LegacyAvatarOptimizerWarning
    {
        static LegacyAvatarOptimizerWarning()
        {
            if (SessionState.GetBool("nadena.dev.legacy-aao-warning", false)) return;
            SessionState.SetBool("nadena.dev.legacy-aao-warning", true);
            EditorApplication.delayCall += DisplayWarning;
        }

        private static void DisplayWarning()
        {
            var isJapanese = true;

            while (true)
            {
                string message, readInOtherLang;
                if (isJapanese)
                {
                    message = "1.4.x以前のAvatar Optimizerがインストールされているようです。\n" +
                              "現在お使いのModular Avatarでは、1.4.x以前のAvatar Optimizerと互換性がありません。\n" +
                              "Avatar Optimizerを1.5.0以降に更新してください！";
                    readInOtherLang = "Read in English";
                }
                else
                {
                    message = "We found Avatar Optimizer 1.4.x or older is installed!\n" +
                              "This version of Modular Avatar is not compatible with Avatar Optimizer 1.4.x or older!\n" +
                              "Please upgrade Avatar Optimizer to 1.5.0 or later!";
                    readInOtherLang = "日本語で読む";
                }
            
                if (EditorUtility.DisplayDialog("Modular Avatar", message, "OK", readInOtherLang))
                    return;

                isJapanese = !isJapanese;
            }
        }
    }
}
#endif
