using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace nadena.dev.modular_avatar.core
{
    public class TestComponent : MonoBehaviour
    {
        public AvatarObjectReference objRef = new AvatarObjectReference();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(TestComponent))]
    class TestComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            TestComponent target = (TestComponent) this.target;

            EditorGUILayout.ObjectField("Current target", target.objRef.Get(target), typeof(GameObject), true);
        }
    }
#endif
}