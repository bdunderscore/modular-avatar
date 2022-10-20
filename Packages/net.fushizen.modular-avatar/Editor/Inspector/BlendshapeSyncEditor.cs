using UnityEditor;
using UnityEngine;

namespace net.fushizen.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarBlendshapeSync))]
    internal class BlendshapeSyncEditor : Editor
    {
        private BlendshapeSelectWindow _window;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Add blendshape"))
            {
                if (_window != null) DestroyImmediate(_window);
                _window = ScriptableObject.CreateInstance<BlendshapeSelectWindow>();
                _window.AvatarRoot = RuntimeUtil.FindAvatarInParents(((ModularAvatarBlendshapeSync) target).transform)
                    .gameObject;
                _window.OfferBinding += OfferBinding;
                _window.Show();
            }
        }

        private void OfferBinding(BlendshapeBinding binding)
        {
            foreach (var obj in targets)
            {
                var sync = (ModularAvatarBlendshapeSync) obj;
                Undo.RecordObject(sync, "Adding blendshape binding");
                if (!sync.Bindings.Contains(binding)) sync.Bindings.Add(binding);
                EditorUtility.SetDirty(sync);
            }
        }
    }
}