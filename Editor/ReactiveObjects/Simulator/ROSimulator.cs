using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.ui;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor.Simulator
{
    internal class ROSimulator : EditorWindow, IHasCustomMenu
    {
        public static PublishedValue<ImmutableDictionary<string, float>> PropertyOverrides = new(null);
        
        internal static string ROOT_PATH = "Packages/nadena.dev.modular-avatar/Editor/ReactiveObjects/Simulator/";
        private static string USS = ROOT_PATH + "ROSimulator.uss";
        private static string UXML = ROOT_PATH + "ROSimulator.uxml";
        private static string EFFECT_GROUP_UXML = ROOT_PATH + "EffectGroup.uxml";

        private ObjectField f_inspecting;
        private VisualElement e_debugInfo;
        private VisualTreeAsset effectGroupTemplate;
        private StyleSheet uss;

        [MenuItem(UnityMenuItems.GameObject_ShowReactionDebugger, false, UnityMenuItems.GameObject_ShowReactionDebuggerOrder)]
        internal static void ShowWindow(MenuCommand command)
        {
            OpenDebugger(command.context as GameObject);
        }

        private void Awake()
        {
            void SetTitle(EditorWindow w)
            {
                w.titleContent = new GUIContent(Localization.L.GetLocalizedString("ro_sim.window.title"));
            }

            LanguagePrefs.RegisterLanguageChangeCallback(this, SetTitle);
            SetTitle(this);
        }

        public static void OpenDebugger(GameObject target)
        {
            var window = GetWindow<ROSimulator>();
            if (window.is_enabled && window.locked) return;

            window.locked = target != Selection.activeGameObject;
            
            // avoid racing with initial creation
            if (window.f_inspecting == null)
            {
                window.LoadUI();
            }
            
            // ReSharper disable once PossibleNullReferenceException
            window.f_inspecting.SetValueWithoutNotify(target);
            window.RefreshUI();
        }
        
        private void OnEnable()
        {
            PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
            EditorApplication.delayCall += LoadUI;
            Selection.selectionChanged += SelectionChanged;
            is_enabled = true;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= SelectionChanged;
            is_enabled = false;

            // Delay this to ensure that we don't try to change this value from within assembly reload callbacks
            // (which generates a noisy exception)
            EditorApplication.delayCall += () =>
            {
                PropertyOverrides.Value = null;
            };
        }
        
        private ComputeContext _lastComputeContext;
        private GameObject currentSelection;
        private GUIStyle lockButtonStyle;
        private bool locked, is_enabled;

        private Dictionary<(int, string), bool> foldoutState = new();
        private Button _btn_clear;

        private void UpdatePropertyOverride(string prop, bool? enable, float f_val)
        {
            if (enable == null)
            {
                PropertyOverrides.Value = PropertyOverrides.Value.Remove(prop);
            } else if (enable.Value)
            {
                PropertyOverrides.Value = PropertyOverrides.Value.SetItem(prop, f_val);
            }
            else
            {
                PropertyOverrides.Value = PropertyOverrides.Value.SetItem(prop, 0f);
            }
            
            EditorApplication.delayCall += RefreshUI;
        }
        
        private void UpdatePropertyOverride(string prop, bool? value)
        {
            if (value == null)
            {
                PropertyOverrides.Value = PropertyOverrides.Value.Remove(prop);
            }
            else
            {
                PropertyOverrides.Value = PropertyOverrides.Value.SetItem(prop, value.Value ? 1f : 0f);
            }
            
            RefreshUI();
        }
        
        private void ShowButton(Rect rect)
        {
            if (lockButtonStyle == null)
            {
                lockButtonStyle = "IN LockButton";
            }
            
            locked = GUI.Toggle(rect, locked, GUIContent.none, lockButtonStyle);
        }

        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Lock"), locked, () => locked = !locked);
        }

        void SelectionChanged()
        {
            if (locked) return;
            
            if (currentSelection != Selection.activeGameObject)
            {
                UpdateSelection();
            }
        }

        private void LoadUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("rootVisualContent");
            uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS);
            
            root.styleSheets.Add(uss);
            var content = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML).CloneTree();
            root.Add(content);
           
            Localization.L.LocalizeUIElements(content);

            root.Q<Button>("debug__reload").clickable.clicked += LoadUI;

            f_inspecting = root.Q<ObjectField>("inspecting");
            f_inspecting.RegisterValueChangedCallback(evt =>
            {
                locked = true;
                UpdateSelection();
            });
            
            _btn_clear = root.Q<Button>("clear-overrides");
            _btn_clear.clickable.clicked += () =>
            {
                PropertyOverrides.Value = ImmutableDictionary<string, float>.Empty;
                RefreshUI();
            };
            
            e_debugInfo = root.Q<VisualElement>("debug-info");
            
            effectGroupTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EFFECT_GROUP_UXML);
            
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            currentSelection = locked ? f_inspecting.value as GameObject : Selection.activeGameObject;
            f_inspecting.SetValueWithoutNotify(currentSelection);

            RefreshUI();
        }

        private void RefreshUI()
        {
            var avatar = RuntimeUtil.FindAvatarInParents(currentSelection?.transform);
            
            if (avatar == null)
            {
                e_debugInfo.style.display = DisplayStyle.None;
                return;
            }
            
            _btn_clear.SetEnabled(!PropertyOverrides.Value.IsEmpty);
            
            e_debugInfo.style.display = DisplayStyle.Flex;

            _lastComputeContext = new ComputeContext("RO Simulator");
            _lastComputeContext.InvokeOnInvalidate(this, MaybeRefreshUI);
            
            var analysis = new ReactiveObjectAnalyzer(_lastComputeContext);
            analysis.ForcePropertyOverrides = PropertyOverrides.Value;
            var result = analysis.Analyze(avatar.gameObject);

            SetThisObjectOverrides(analysis);
            SetOverallActiveHeader(currentSelection, result.InitialStates);
            SetAffectedBy(currentSelection, result.Shapes);
        }
        
        private static void MaybeRefreshUI(ROSimulator self)
        {
            if (self.is_enabled)
            {
                self.RefreshUI();
            }
        }

        private void SetThisObjectOverrides(ReactiveObjectAnalyzer analysis)
        {
            BindOverrideToParameter("this-obj-override", analysis.GetGameObjectStateProperty(currentSelection), 1);
            BindOverrideToParameter("this-menu-override", analysis.GetMenuItemProperty(currentSelection),
                currentSelection.GetComponent<ModularAvatarMenuItem>()?.Control?.value ?? 1
            );
        }

        private void BindOverrideToParameter(string overrideElemName, string property, float targetValue)
        {
            var elem = e_debugInfo.Q<VisualElement>(overrideElemName);
            var soc = elem.Q<StateOverrideController>();

            if (property == null)
            {
                elem.style.display = DisplayStyle.None;
                return;
            }
            elem.style.display = DisplayStyle.Flex;
            
            if (PropertyOverrides.Value.TryGetValue(property, out var overrideValue))
            {
                soc.SetWithoutNotify(Mathf.Approximately(overrideValue, targetValue));
            }
            else
            {
                soc.SetWithoutNotify(null);
            }
            
            soc.OnStateOverrideChanged += value =>
            {
                UpdatePropertyOverride(property, value, targetValue);
            };
        }

        private void SetAffectedBy(GameObject gameObject, Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var effect_list = e_debugInfo.Q<ScrollView>("effect-list");
            effect_list.Clear();

            var orderedShapes = shapes.Values
                .Where(s => AffectedBy(s.TargetProp, gameObject))
                .OrderBy(
                s =>
                {
                    if (s.TargetProp.TargetObject is GameObject go && s.TargetProp.PropertyName == "m_IsActive")
                        return (0, null, null);
                    return (1, s.TargetProp.TargetObject.GetType().ToString(), s.TargetProp.PropertyName);
                }
            );
            
            foreach (var shape in orderedShapes)
            {
                var targetProp = shape.TargetProp;
                var propInfo = shape;

                var propGroup = new Foldout();
                propGroup.text = targetProp.TargetObject.GetType() + "." + targetProp.PropertyName;
                var foldoutStateKey = (shape.TargetProp.TargetObject?.GetInstanceID() ?? -1, shape.TargetProp.PropertyName);
                propGroup.RegisterValueChangedCallback(evt =>
                {
                    foldoutState[foldoutStateKey] = evt.newValue;
                    if (evt.newValue)
                    {
                        propGroup.AddToClassList("foldout-open");
                    }
                    else
                    {
                        propGroup.RemoveFromClassList("foldout-open");
                    }
                });
                if (shape.TargetProp.TargetObject is GameObject go && shape.TargetProp.PropertyName == "m_IsActive")
                {
                    propGroup.text = "Active State";
                    propGroup.value = true;
                }
                else
                {
                    propGroup.value = false;
                }
                effect_list.Add(propGroup);
                if (foldoutState.TryGetValue(foldoutStateKey, out var state))
                {
                    propGroup.value = state;
                }
                if (propGroup.value)
                {
                    propGroup.AddToClassList("foldout-open");
                }
                else
                {
                    propGroup.RemoveFromClassList("foldout-open");
                }

                foreach (var reactionRule in propInfo.actionGroups)
                {
                    if (reactionRule.ControllingObject == null) continue;
                    
                    var effectGroup = effectGroupTemplate.CloneTree();
                    propGroup.Add(effectGroup);
                    effectGroup.styleSheets.Add(uss);
                    
                    if (reactionRule.InitiallyActive)
                    {
                        effectGroup.AddToClassList("st-active");
                    }

                    var source = effectGroup.Q<ObjectField>("effect__source");
                    source.SetEnabled(false);
                    source.SetValueWithoutNotify(reactionRule.ControllingObject);
                    
                    var conditions = effectGroup.Q<VisualElement>("controlling-conditions");
                    BuildRuleConditionBlock(conditions, reactionRule);
                    
                    // For our TextFields, we want to localize the label, not the text, so we'll do this manually...
                    foreach (var field in effectGroup.Query<VisualElement>(classes:"ndmf-tr-label").ToList())
                    {
                        var labelProp = field.GetType().GetProperty("label");
                        var tooltipProp = field.GetType().GetProperty("tooltip");

                        if (labelProp == null) continue;
                        
                        var key = labelProp.GetValue(field) as string;
                        Action<VisualElement> relocalize = f2 =>
                        {
                            labelProp.SetValue(f2, Localization.L.GetLocalizedString(key));
                            tooltipProp?.SetValue(f2, Localization.L.GetLocalizedString(key + ".tooltip"));
                        };

                        relocalize(field);
                        LanguagePrefs.RegisterLanguageChangeCallback(field, relocalize);
                        
                        field.RemoveFromClassList("ndmf-tr-label");
                    }
                    
                    // Localize once we've built the condition blocks as they contain dynamically created translated
                    // strings.
                    Localization.L.LocalizeUIElements(effectGroup);
                    
                    var f_target_component = effectGroup.Q<ObjectField>("effect__target");
                    var f_property = effectGroup.Q<TextField>("effect__prop");
                    var f_set_active = effectGroup.Q<VisualElement>("effect__set-active");
                    var f_set_inactive = effectGroup.Q<VisualElement>("effect__set-inactive");
                    var f_value = effectGroup.Q<FloatField>("effect__value");
                    var f_material = effectGroup.Q<ObjectField>("effect__material");
                    var f_delete = effectGroup.Q("effect__deleted");
                    
                    f_target_component.style.display = DisplayStyle.None;
                    f_target_component.SetEnabled(false);
                    f_property.style.display = DisplayStyle.None;
                    f_property.SetEnabled(false);
                    f_set_active.style.display = DisplayStyle.None;
                    f_set_inactive.style.display = DisplayStyle.None;
                    f_value.style.display = DisplayStyle.None;
                    f_value.SetEnabled(false);
                    f_material.style.display = DisplayStyle.None;
                    f_material.SetEnabled(false);
                    f_delete.style.display = DisplayStyle.None;
                    f_delete.SetEnabled(false);
                    
                    if (targetProp.TargetObject is GameObject && targetProp.PropertyName == "m_IsActive")
                    {
                        if (((float)reactionRule.Value) > 0.5f)
                        {
                            f_set_active.style.display = DisplayStyle.Flex;
                        }
                        else
                        {
                            f_set_inactive.style.display = DisplayStyle.Flex;
                        }
                    }
                    else
                    {
                        f_target_component.SetValueWithoutNotify(targetProp.TargetObject);
                        f_target_component.style.display = DisplayStyle.Flex;
                        f_property.value = targetProp.PropertyName;
                        f_property.style.display = DisplayStyle.Flex;

                        if (reactionRule.IsDelete)
                        {
                            f_delete.style.display = DisplayStyle.Flex;
                        } else if (reactionRule.Value is float f)
                        {
                            f_value.SetValueWithoutNotify(f);
                            f_value.style.display = DisplayStyle.Flex;
                        } else if (reactionRule.Value is Material m)
                        {
                            f_material.SetValueWithoutNotify(m);
                            f_material.style.display = DisplayStyle.Flex;
                        }
                    } 
                }
            }
        }

        private bool AffectedBy(TargetProp shapeKey, GameObject gameObject)
        {
            return (shapeKey.TargetObject == gameObject) ||
                   (shapeKey.TargetObject is Component c && c.gameObject == gameObject);
        }

        private void BuildRuleConditionBlock(VisualElement conditions, ReactionRule rule)
        {
            if (rule.Inverted)
            {
                conditions.AddToClassList("rule-inverted");
            }

            foreach (var condition in rule.ControllingConditions)
            {
                var conditionElem = new VisualElement();
                conditions.Add(conditionElem);
                conditionElem.AddToClassList("controlling-condition");

                var soc = new StateOverrideController();
                conditionElem.Add(soc);

                var prop = condition.Parameter;
                if (PropertyOverrides.Value.TryGetValue(prop, out var overrideValue))
                {
                    soc.SetWithoutNotify(condition, overrideValue);
                }

                float targetValue;

                if (!float.IsFinite(condition.ParameterValueHi))
                {
                    targetValue = Mathf.Round(condition.ParameterValueLo + 0.5f);
                } else if (!float.IsFinite(condition.ParameterValueLo))
                {
                    targetValue = Mathf.Round(condition.ParameterValueHi - 0.5f);
                }
                else
                {
                    targetValue = Mathf.Round((condition.ParameterValueLo + condition.ParameterValueHi) / 2);
                }
                
                soc.OnStateOverrideChanged += value => UpdatePropertyOverride(prop, value, targetValue);
                
                var active = condition.InitiallyActive;
                var active_label = active ? "active" : "inactive";
                active_label = "ro_sim.state." + active_label;
                var active_classname = active ? "source-active" : "source-inactive";

                switch (condition.DebugReference)
                {
                    case GameObject go:
                    {
                        var controller = new ObjectField(active_label);
                        controller.SetEnabled(false);
                        controller.SetValueWithoutNotify(go);
                        controller.AddToClassList(active_classname);
                        controller.AddToClassList("ndmf-tr");
                        conditionElem.Add(controller);
                        break;
                    }
                    case Component c:
                    {
                        var controller = new ObjectField(active_label);
                        controller.SetEnabled(false);
                        controller.SetValueWithoutNotify(c);
                        controller.AddToClassList(active_classname);
                        controller.AddToClassList("ndmf-tr");
                        conditionElem.Add(controller);
                        break;
                    }
                    default:
                    {
                        var controller = new TextField(active_label);
                        controller.SetEnabled(false);
                        controller.value = condition.DebugReference.ToString();
                        controller.AddToClassList(active_classname);
                        controller.AddToClassList("ndmf-tr");
                        conditionElem.Add(controller);
                        break;
                    }
                }
            }
        }

        private void SetOverallActiveHeader(GameObject obj, Dictionary<TargetProp, object> initialStates)
        {
            bool activeState = obj.activeInHierarchy;
            if (initialStates.TryGetValue(TargetProp.ForObjectActive(obj), out var activeStateObj))
            {
                activeState = ((float)activeStateObj) > 0;
            }
            var ve_active = e_debugInfo.Q<VisualElement>("state-enabled");
            var ve_inactive = e_debugInfo.Q<VisualElement>("state-disabled");
            
            ve_active.style.display = activeState ? DisplayStyle.Flex : DisplayStyle.None;
            ve_inactive.style.display = activeState ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}