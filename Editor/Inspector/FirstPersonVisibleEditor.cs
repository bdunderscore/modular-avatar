using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarVisibleHeadAccessory))]
    internal class FirstPersonVisibleEditor : MAEditorBase
    {
        private VisibleHeadAccessoryValidation _validation;

        private void OnEnable()
        {
            var target = (ModularAvatarVisibleHeadAccessory) this.target;
            var avatar = RuntimeUtil.FindAvatarTransformInParents(target.transform);

            if (avatar != null) _validation = new VisibleHeadAccessoryValidation(avatar.gameObject);
        }

        protected override void OnInnerInspectorGUI()
        {
            var target = (ModularAvatarVisibleHeadAccessory) this.target;

            if (_validation != null)
            {
                var status = _validation.Validate(target);

                switch (status)
                {
                    case VisibleHeadAccessoryValidation.ReadyStatus.Ready:
                    case VisibleHeadAccessoryValidation.ReadyStatus.ParentMarked:
                        EditorGUILayout.HelpBox(Localization.S("fpvisible.normal"), MessageType.Info);
                        break;
                    case VisibleHeadAccessoryValidation.ReadyStatus.NotUnderHead:
                        EditorGUILayout.HelpBox(Localization.S("fpvisible.NotUnderHead"), MessageType.Warning);
                        break;
                    default:
                    {
                        var label = "fpvisible." + status;
                        EditorGUILayout.HelpBox(Localization.S(label), MessageType.Error);
                        break;
                    }
                }
            }

            Localization.ShowLanguageUI();
        }
    }
}
