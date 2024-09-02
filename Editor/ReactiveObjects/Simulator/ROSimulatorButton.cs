using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.Simulator;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ROSimulatorButton : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ROSimulatorButton, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }

        private Button btn;
        public UnityEngine.Object ReferenceObject;
        
        public static void BindRefObject(VisualElement elem, UnityEngine.Object obj)
        {
            var button = elem.Q<ROSimulatorButton>();
            
            if (button != null)
            {
                button.ReferenceObject = obj;
            }
        }
        
        public ROSimulatorButton()
        {
            btn = new Button();
            btn.AddToClassList("ndmf-tr");
            btn.text = "ro_sim.open_debugger_button";
            
            Add(btn);
            
            btn.clicked += OpenDebugger;
        }

        private void OpenDebugger()
        {
            GameObject target = Selection.activeGameObject;
            if (ReferenceObject is Component c) target = c.gameObject;
            else if (ReferenceObject is GameObject go) target = go;

            ROSimulator.OpenDebugger(target);
        }
    }
}