using System;
using UnityEditor;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarFirstPersonVisible))]
    public class FirstPersonVisibleEditor : Editor
    {
        private FirstPersonVisibleProcessor _processor;

        private void OnEnable()
        {
            var target = (ModularAvatarFirstPersonVisible) this.target;
            var avatar = RuntimeUtil.FindAvatarInParents(target.transform);

            if (avatar != null) _processor = new FirstPersonVisibleProcessor(avatar);
        }

        public override void OnInspectorGUI()
        {
            var target = (ModularAvatarFirstPersonVisible) this.target;

#if UNITY_ANDROID
            EditorGUILayout.HelpBox(Localization.S("fpvisible.quest"), MessageType.Warning);

#else

            if (_processor != null)
            {
                var status = _processor.Validate(target);

                switch (status)
                {
                    case FirstPersonVisibleProcessor.ReadyStatus.Ready:
                    case FirstPersonVisibleProcessor.ReadyStatus.ParentMarked:
                        EditorGUILayout.HelpBox(Localization.S("fpvisible.normal"), MessageType.Info);
                        break;
                    default:
                    {
                        var label = "fpvisible." + status;
                        EditorGUILayout.HelpBox(Localization.S(label), MessageType.Error);
                        break;
                    }
                }
            }

#endif

            Localization.ShowLanguageUI();
        }
    }
}