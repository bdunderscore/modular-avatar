using nadena.dev.modular_avatar.core.editor.Simulator;
using UnityEditor;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class StateOverrideController : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<StateOverrideController, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }

        private static StyleSheet uss;
        private Button btn_disable, btn_default, btn_enable;
        public System.Action<bool?> OnStateOverrideChanged;
        
        public StateOverrideController()
        {
            uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ROSimulator.ROOT_PATH + "StateOverrideController.uss");
            styleSheets.Add(uss);
            
            AddToClassList("state-override-controller");

            btn_disable = new Button();
            btn_disable.AddToClassList("btn-disable");
            btn_default = new Button();
            btn_default.AddToClassList("btn-default");
            btn_enable = new Button();
            btn_enable.AddToClassList("btn-enable");

            btn_disable.text = "-";
            btn_default.text = " ";
            btn_enable.text = "+";
            
            btn_disable.clicked += () => SetStateOverride(false);
            btn_default.clicked += () => SetStateOverride(null);
            btn_enable.clicked += () => SetStateOverride(true);

            Add(btn_disable);
            Add(btn_default);
            Add(btn_enable);
        }
        
        private void SetStateOverride(bool? state)
        {
            SetWithoutNotify(state);
            OnStateOverrideChanged?.Invoke(state);
        }

        public void SetWithoutNotify(bool? state)
        {
            RemoveFromClassList("override-enable");
            RemoveFromClassList("override-disable");
            RemoveFromClassList("override-default");
            
            if (state == null) AddToClassList("override-default");
            else if (state == true) AddToClassList("override-enable");
            else AddToClassList("override-disable");
        }

        public void SetWithoutNotify(ControlCondition condition, float value)
        {
            if (value >= condition.ParameterValueLo && value <= condition.ParameterValueHi)
            {
                SetWithoutNotify(true);
            }
            else
            {
                SetWithoutNotify(false);
            }
        }
    }
}