#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using nadena.dev.modular_avatar.core.menu;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static nadena.dev.modular_avatar.core.editor.Localization;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(SubmenuSource))]
    class SubmenuSourceDrawer : EnumDrawer<SubmenuSource>
    {
        protected override string localizationPrefix => "submenu_source";
    }

    internal static class ParameterIntrospectionCache
    {
        internal static PropCache<GameObject, ImmutableList<ProvidedParameter>> ProvidedParameterCache =
            new("GetParametersForObject", GetParametersForObject_miss);

        internal static PropCache<GameObject, ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>>
            ParameterRemappingCache = new("GetParameterRemappingsAt", GetParameterRemappingsAt_miss);

        private static ImmutableList<ProvidedParameter> GetParametersForObject_miss(ComputeContext ctx, GameObject obj)
        {
            if (obj == null) return ImmutableList<ProvidedParameter>.Empty;

            return ParameterInfo.ForPreview(ctx).GetParametersForObject(obj).ToImmutableList();
        }

        private static ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>
            GetParameterRemappingsAt_miss(ComputeContext ctx, GameObject obj)
        {
            if (obj == null) return ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>.Empty;

            return ParameterInfo.ForPreview(ctx).GetParameterRemappingsAt(obj);
        }
        
        internal static ImmutableList<ProvidedParameter> GetParametersForObject(GameObject avatar)
        {
            return ProvidedParameterCache.Get(ComputeContext.NullContext, avatar);
        }
        
        internal static ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> GetParameterRemappingsAt(GameObject avatar)
        {
            return ParameterRemappingCache.Get(ComputeContext.NullContext, avatar);
        }
    }

    internal class MenuItemCoreGUI
    {
        private static readonly ObjectIDGenerator IdGenerator = new ObjectIDGenerator();
        private readonly GameObject _parameterReference;
        private readonly Action _redraw;

        private readonly SerializedObject _obj;

        private readonly SerializedProperty _name;
        private readonly SerializedProperty _texture;
        private readonly SerializedProperty _type;
        private readonly SerializedProperty _value;
        private readonly SerializedProperty _submenu;

        private readonly ParameterGUI _parameterGUI;
        private readonly SerializedProperty _parameterName;

        private readonly SerializedProperty _subParamsRoot;
        private readonly SerializedProperty _labelsRoot;

        private readonly MenuPreviewGUI _previewGUI;

        private ParameterGUI[] _subParams;
        private SerializedProperty[] _labels;

        private int texPicker = -1;

        private readonly SerializedProperty _prop_submenuSource;
        private readonly SerializedProperty _prop_otherObjSource;

        private readonly SerializedProperty _prop_isSynced;
        private readonly SerializedProperty _prop_isSaved;
        private readonly SerializedProperty _prop_isDefault;
        private readonly SerializedProperty _prop_automaticValue;
        
        public bool AlwaysExpandContents = false;
        public bool ExpandContents = false;

        private readonly Dictionary<string, ProvidedParameter> _knownParameters = new();
        private bool _parameterSourceNotDetermined;

        public MenuItemCoreGUI(SerializedObject obj, Action redraw)
        {
            _obj = obj;

            GameObject parameterReference = null;
            if (obj.targetObjects.Length == 1)
            {
                parameterReference = (obj.targetObject as Component)?.gameObject;
            }

            _parameterReference = parameterReference;
            _redraw = redraw;

            InitKnownParameters();

            var gameObjects = new SerializedObject(
                obj.targetObjects.Select(o =>
                    (Object) ((ModularAvatarMenuItem) o).gameObject
                ).ToArray()
            );

            _name = gameObjects.FindProperty("m_Name");

            var control = obj.FindProperty(nameof(ModularAvatarMenuItem.Control));

            _texture = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.icon));
            _type = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            _parameterName = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            
            _value = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));
            _submenu = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subMenu));

            _parameterGUI = new ParameterGUI(parameterReference, _parameterName, redraw);

            _subParamsRoot = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subParameters));
            _labelsRoot = control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.labels));

            _prop_submenuSource = obj.FindProperty(nameof(ModularAvatarMenuItem.MenuSource));
            _prop_otherObjSource = obj.FindProperty(nameof(ModularAvatarMenuItem.menuSource_otherObjectChildren));

            _prop_isSynced = obj.FindProperty(nameof(ModularAvatarMenuItem.isSynced));
            _prop_isSaved = obj.FindProperty(nameof(ModularAvatarMenuItem.isSaved));
            _prop_isDefault = obj.FindProperty(nameof(ModularAvatarMenuItem.isDefault));
            _prop_automaticValue = obj.FindProperty(nameof(ModularAvatarMenuItem.automaticValue));
            
            _previewGUI = new MenuPreviewGUI(redraw);
        }

        private void InitKnownParameters()
        {
            var paramRef = _parameterReference;
            if (_parameterReference == null)
                // TODO: This could give incorrect results in some cases when we have multiple objects selected with
                // different rename contexts.
                paramRef = (_obj.targetObjects[0] as Component)?.gameObject;

            if (paramRef == null)
            {
                _parameterSourceNotDetermined = true;
                return;
            }

            var parentAvatar = RuntimeUtil.FindAvatarInParents(paramRef.transform);
            if (parentAvatar == null)
            {
                _parameterSourceNotDetermined = true;
                return;
            }
            
            Dictionary<string, ProvidedParameter> rootParameters = new();
            
            foreach (var param in ParameterIntrospectionCache.GetParametersForObject(parentAvatar.gameObject)
                         .Where(p => p.Namespace == ParameterNamespace.Animator)
                    )
                rootParameters[param.EffectiveName] = param;

            var remaps = ParameterIntrospectionCache.GetParameterRemappingsAt(paramRef);
            foreach (var remap in remaps)
            {
                if (remap.Key.Item1 != ParameterNamespace.Animator) continue;
                if (rootParameters.ContainsKey(remap.Value.ParameterName))
                    _knownParameters[remap.Key.Item2] = rootParameters[remap.Value.ParameterName];
            }

            foreach (var rootParam in rootParameters)
                if (!remaps.ContainsKey((ParameterNamespace.Animator, rootParam.Key)))
                    _knownParameters[rootParam.Key] = rootParam.Value;
        }

        /// <summary>
        /// Builds a menu item GUI for a raw VRCExpressionsMenu.Control reference.
        /// </summary>
        /// <param name="parameterReference"></param>
        /// <param name="_control"></param>
        /// <param name="redraw"></param>
        public MenuItemCoreGUI(GameObject parameterReference, SerializedProperty _control, Action redraw)
        {
            _obj = _control.serializedObject;
            _parameterReference = parameterReference;
            InitKnownParameters();
            
            _redraw = redraw;
            _name = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.name));
            _texture = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.icon));
            _type = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.type));
            _parameterName = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter))
                .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.parameter.name));
            
            _value = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.value));
            _submenu = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subMenu));

            _parameterGUI = new ParameterGUI(parameterReference, _parameterName, redraw);

            _subParamsRoot = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.subParameters));
            _labelsRoot = _control.FindPropertyRelative(nameof(VRCExpressionsMenu.Control.labels));

            _prop_isSynced = _control.FindPropertyRelative(nameof(ModularAvatarMenuItem.isSynced));
            _prop_isSaved = _control.FindPropertyRelative(nameof(ModularAvatarMenuItem.isSaved));
            _prop_isDefault = _control.FindPropertyRelative(nameof(ModularAvatarMenuItem.isDefault));
            _prop_automaticValue = null;

            _prop_submenuSource = null;
            _prop_otherObjSource = null;
            _previewGUI = new MenuPreviewGUI(redraw);
        }

        private void DrawHorizontalToggleProp(
            SerializedProperty prop,
            GUIContent label,
            bool? forceMixedValues = null,
            bool? forceValue = null
        )
        {
            var toggleSize = EditorStyles.toggle.CalcSize(new GUIContent());
            var labelSize = EditorStyles.label.CalcSize(label);
            var width = toggleSize.x + labelSize.x + 4;

            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(width));
            EditorGUI.BeginProperty(rect, label, prop);

            if (forceMixedValues != null) EditorGUI.showMixedValue = forceMixedValues.Value;

            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.ToggleLeft(rect, label, forceValue ?? prop.boolValue);
            if (EditorGUI.EndChangeCheck()) prop.boolValue = value;
            
            EditorGUI.EndProperty();
        }
        
        public void DoGUI()
        {
            if (_obj != null) _obj.UpdateIfRequiredOrScript();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_name, G("menuitem.prop.name"));
            if (EditorGUI.EndChangeCheck())
            {
                _name.serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.PropertyField(_texture, G("menuitem.prop.icon"));
            EditorGUILayout.PropertyField(_type, G("menuitem.prop.type"));
            DoValueField();

            _parameterGUI.DoGUI(true);

            ShowInnateParameterGUI();

            EditorGUILayout.EndVertical();

            if (_texture != null)
            {
                var tex = _texture.objectReferenceValue as Texture2D;
                if (tex != null && !_texture.hasMultipleDifferentValues)
                {
                    var size = EditorGUIUtility.singleLineHeight * 5;
                    var margin = 4;
                    var withMargin = new Vector2(margin + size, margin + size);

                    var rect = GUILayoutUtility.GetRect(withMargin.x, withMargin.y, GUILayout.ExpandWidth(false),
                        GUILayout.ExpandHeight(true));
                    rect.x += margin;
                    rect.y = rect.y + rect.height / 2 - size / 2;
                    rect.width = size;
                    rect.height = size;

                    GUI.Box(rect, new GUIContent(), "flow node 1");
                    GUI.DrawTexture(rect, tex);
                }
            }

            EditorGUILayout.EndHorizontal();

            try
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();

                if (_type.hasMultipleDifferentValues) return;
                VRCExpressionsMenu.Control.ControlType type =
                    (VRCExpressionsMenu.Control.ControlType) Enum
                        .GetValues(typeof(VRCExpressionsMenu.Control.ControlType))
                        .GetValue(_type.enumValueIndex);

                switch (type)
                {
                    case VRCExpressionsMenu.Control.ControlType.Button:
                    case VRCExpressionsMenu.Control.ControlType.Toggle:
                        return;
                }

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                switch (type)
                {
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    {
                        object menuSource = null;
                        bool canExpand = false;

                        if (_prop_submenuSource != null)
                        {
                            EditorGUILayout.PropertyField(_prop_submenuSource, G("menuitem.prop.submenu_source"));
                            if (_prop_submenuSource.hasMultipleDifferentValues) break;

                            var sourceType = (SubmenuSource) Enum.GetValues(typeof(SubmenuSource))
                                .GetValue(_prop_submenuSource.enumValueIndex);

                            switch (sourceType)
                            {
                                case SubmenuSource.Children:
                                {
                                    EditorGUILayout.PropertyField(_prop_otherObjSource,
                                        G("menuitem.prop.source_override"));
                                    if (_prop_otherObjSource.hasMultipleDifferentValues) break;
                                    if (_prop_otherObjSource.objectReferenceValue == null)
                                    {
                                        if (_obj.targetObjects.Length != 1) break;
                                        menuSource = new MenuNodesUnder((_obj.targetObject as Component)?.gameObject);
                                    }
                                    else
                                    {
                                        menuSource =
                                            new MenuNodesUnder((GameObject) _prop_otherObjSource.objectReferenceValue);
                                    }

                                    break;
                                }
                                case SubmenuSource.MenuAsset:
                                {
                                    EditorGUILayout.PropertyField(_submenu, G("menuitem.prop.submenu_asset"));
                                    canExpand = true;

                                    if (_submenu.hasMultipleDifferentValues) break;
                                    menuSource = _submenu.objectReferenceValue;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Native VRCSDK control
                            EditorGUILayout.PropertyField(_submenu, G("menuitem.prop.submenu_asset"));
                            if (_submenu.hasMultipleDifferentValues) break;
                            menuSource = _submenu.objectReferenceValue;
                        }

                        if (menuSource != null)
                        {
                            if (AlwaysExpandContents)
                            {
                                ExpandContents = true;
                            }
                            else
                            {
                                EditorGUI.indentLevel += 1;
                                ExpandContents = EditorGUILayout.Foldout(ExpandContents, G("menuitem.showcontents"));
                                EditorGUI.indentLevel -= 1;
                            }

                            if (ExpandContents)
                            {
                                if (menuSource is VRCExpressionsMenu menu) _previewGUI.DoGUI(menu, _parameterReference);
                                else if (menuSource is MenuSource nodes) _previewGUI.DoGUI(nodes);
                            }
                        }

                        if (canExpand && (_submenu.hasMultipleDifferentValues || _submenu.objectReferenceValue != null))
                        {
                            if (GUILayout.Button(G("menuitem.misc.extract")))
                            {
                                _obj.ApplyModifiedProperties();

                                foreach (var targetObj in _obj.targetObjects)
                                {
                                    var menuItem = (ModularAvatarMenuItem) targetObj;
                                    if (menuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu
                                        && menuItem.Control.subMenu != null
                                        && menuItem.MenuSource == SubmenuSource.MenuAsset
                                       )
                                    {
                                        Undo.RecordObject(menuItem, "Extract menu");
                                        MenuExtractor.ExtractSingleLayerMenu(menuItem.Control.subMenu,
                                            menuItem.gameObject);
                                        menuItem.Control.subMenu = null;
                                        menuItem.MenuSource = SubmenuSource.Children;
                                        menuItem.menuSource_otherObjectChildren = null;
                                        EditorUtility.SetDirty(menuItem);
                                        PrefabUtility.RecordPrefabInstancePropertyModifications(menuItem);
                                    }
                                }

                                _obj.Update();
                            }
                        }

                        break;
                    }
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    {
                        EnsureParameterCount(1);

                        _subParams[0].DoGUI(true,
                            G("menuitem.param.rotation"));

                        break;
                    }
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    {
                        EnsureParameterCount(2);
                        EnsureLabelCount(4);

                        EditorGUILayout.LabelField(G("menuitem.label.parameters"), EditorStyles.boldLabel);
                        EditorGUILayout.Space(2);

                        _subParams[0].DoGUI(true,
                            G("menuitem.param.horizontal"));
                        _subParams[1].DoGUI(true,
                            G("menuitem.param.vertical"));

                        DoFourAxisLabels(false);

                        break;
                    }

                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    {
                        DoFourAxisLabels(true);
                        break;
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                _obj.ApplyModifiedProperties();
            }
        }

        private void ShowInnateParameterGUI()
        {
            if (_prop_isDefault == null)
                // This is probably coming from a VRC Expressions menu asset.
                // For now, don't show the UI in this case.
                return;

            var multipleSelections = _obj.targetObjects.Length > 1;

            var paramName = _parameterName.stringValue;
            var siblings = FindSiblingMenuItems(_obj);

            EditorGUILayout.BeginHorizontal();

            var forceMixedValues = _parameterName.hasMultipleDifferentValues;

            var syncedIsMixed = forceMixedValues || _prop_isSynced.hasMultipleDifferentValues ||
                                siblings != null && siblings.Any(s => s.isSynced != _prop_isSynced.boolValue);
            var savedIsMixed = forceMixedValues || _prop_isSaved.hasMultipleDifferentValues ||
                               siblings != null && siblings.Any(s => s.isSaved != _prop_isSaved.boolValue);
            
            var knownParameter = _parameterName.hasMultipleDifferentValues
                ? null
                : _knownParameters.GetValueOrDefault(paramName);

            var knownSource = knownParameter?.Source;
            var externalSource = knownSource != null && knownSource is not ModularAvatarMenuItem;

            if (externalSource) savedIsMixed = true; // NDMF doesn't yet support querying for the saved state
            var forceSyncedValue = externalSource ? knownParameter?.WantSynced : null;

            var knownParamDefault = knownParameter?.DefaultValue;
            var isDefaultByKnownParam =
                knownParamDefault != null ? _value.floatValue == knownParamDefault : (bool?)null;

            if (knownParameter != null && knownParameter.Source is ModularAvatarMenuItem)
                isDefaultByKnownParam = null;

            if (_prop_automaticValue?.boolValue == true) isDefaultByKnownParam = null;

            Object controller = knownParameter?.Source;

            // If we can't figure out what to reference the parameter names to, or if they're controlled by something
            // other than the Menu Item component itself, disable the UI
            var controllerIsElsewhere = externalSource || _parameterSourceNotDetermined;

            using (new EditorGUI.DisabledScope(
                       _parameterName.hasMultipleDifferentValues || controllerIsElsewhere)
                  )
            {
                // If we have multiple menu items selected, it probably doesn't make sense to make them all default.
                // But, we do want to see if _any_ are default.
                var anyIsDefault = _prop_isDefault.hasMultipleDifferentValues || _prop_isDefault.boolValue;
                var mixedIsDefault = multipleSelections && anyIsDefault;
                using (new EditorGUI.DisabledScope(multipleSelections || isDefaultByKnownParam != null))
                {
                    EditorGUI.BeginChangeCheck();
                    DrawHorizontalToggleProp(_prop_isDefault, G("menuitem.prop.is_default"), mixedIsDefault,
                        isDefaultByKnownParam);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _obj.ApplyModifiedProperties();
                        ClearConflictingDefaults(siblings);
                    }
                }

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                DrawHorizontalToggleProp(_prop_isSaved, G("menuitem.prop.is_saved"), savedIsMixed);
                if (EditorGUI.EndChangeCheck() && siblings != null)
                    foreach (var sibling in siblings)
                    {
                        sibling.isSaved = _prop_isSaved.boolValue;
                        EditorUtility.SetDirty(sibling);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(sibling);
                    }

                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                DrawHorizontalToggleProp(_prop_isSynced, G("menuitem.prop.is_synced"), syncedIsMixed,
                    forceSyncedValue);
                if (EditorGUI.EndChangeCheck() && siblings != null)
                    foreach (var sibling in siblings)
                    {
                        sibling.isSynced = _prop_isSynced.boolValue;
                        EditorUtility.SetDirty(sibling);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(sibling);
                    }
            }

            if (controllerIsElsewhere)
            {
                var refStyle = EditorStyles.toggle;
                var refContent = new GUIContent("test");
                var refRect = refStyle.CalcSize(refContent);
                var height = refRect.y + EditorStyles.toggle.margin.top + EditorStyles.toggle.margin.bottom;

                GUILayout.FlexibleSpace();
                var style = new GUIStyle(EditorStyles.miniButton);
                style.fixedWidth = 0;
                style.fixedHeight = 0;
                style.stretchHeight = true;
                style.stretchWidth = true;
                style.imagePosition = ImagePosition.ImageOnly;
                var icon = EditorGUIUtility.FindTexture("d_Search Icon");

                var rect = GUILayoutUtility.GetRect(new GUIContent(), style, GUILayout.ExpandWidth(false),
                    GUILayout.Width(height), GUILayout.Height(height));

                if (GUI.Button(rect, new GUIContent(), style))
                {
                    if (controller is VRCAvatarDescriptor desc) controller = desc.expressionParameters;
                    Selection.activeObject = controller;
                    EditorGUIUtility.PingObject(controller);
                }

                rect.xMin += 2;
                rect.yMin += 2;
                rect.xMax -= 2;
                rect.yMax -= 2;
                GUI.DrawTexture(rect, icon);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DoValueField()
        {
            var value_label = G("menuitem.prop.value");
            var auto_label = G("menuitem.prop.automatic_value");

            if (_prop_automaticValue == null)
            {
                EditorGUILayout.PropertyField(_value, value_label);
                return;
            }

            var toggleSize = EditorStyles.toggle.CalcSize(new GUIContent());
            var autoLabelSize = EditorStyles.label.CalcSize(auto_label);

            var style = EditorStyles.numberField;
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, style);

            var valueRect = rect;
            valueRect.xMax -= toggleSize.x + autoLabelSize.x + 4;

            var autoRect = rect;
            autoRect.xMin = valueRect.xMax + 4;

            var suppressValue = _prop_automaticValue.boolValue || _prop_automaticValue.hasMultipleDifferentValues;

            using (new EditorGUI.DisabledScope(suppressValue))
            {
                if (suppressValue)
                {
                    EditorGUI.TextField(valueRect, value_label, "", style);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(valueRect, _value, value_label);
                    if (EditorGUI.EndChangeCheck()) _prop_automaticValue.boolValue = false;
                }
            }

            EditorGUI.BeginProperty(autoRect, auto_label, _prop_automaticValue);
            EditorGUI.BeginChangeCheck();

            EditorGUI.showMixedValue = _prop_automaticValue.hasMultipleDifferentValues;
            var autoValue = EditorGUI.ToggleLeft(autoRect, auto_label, _prop_automaticValue.boolValue);

            if (EditorGUI.EndChangeCheck()) _prop_automaticValue.boolValue = autoValue;
            EditorGUI.EndProperty();
        }

        private List<ModularAvatarMenuItem> FindSiblingMenuItems(SerializedObject serializedObject)
        {
            if (serializedObject == null || serializedObject.isEditingMultipleObjects) return null;

            var myMenuItem = serializedObject.targetObject as ModularAvatarMenuItem;
            if (myMenuItem == null) return null;

            var myParameterName = myMenuItem.Control.parameter.name;
            if (string.IsNullOrEmpty(myParameterName)) return new List<ModularAvatarMenuItem>();

            var myMappings = ParameterIntrospectionCache.GetParameterRemappingsAt(myMenuItem.gameObject);
            if (myMappings.TryGetValue((ParameterNamespace.Animator, myParameterName), out var myReplacement))
                myParameterName = myReplacement.ParameterName;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(myMenuItem.gameObject.transform);
            var siblings = new List<ModularAvatarMenuItem>();

            foreach (var otherMenuItem in avatarRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                if (otherMenuItem == myMenuItem) continue;

                var otherParameterName = otherMenuItem.Control.parameter.name;
                if (string.IsNullOrEmpty(otherParameterName)) continue;

                var otherMappings = ParameterIntrospectionCache.GetParameterRemappingsAt(otherMenuItem.gameObject);
                if (otherMappings.TryGetValue((ParameterNamespace.Animator, otherParameterName),
                        out var otherReplacement))
                    otherParameterName = otherReplacement.ParameterName;

                if (otherParameterName != myParameterName) continue;

                siblings.Add(otherMenuItem);
            }

            return siblings;
        }

        private void ClearConflictingDefaults(List<ModularAvatarMenuItem> siblingItems)
        {
            var siblings = siblingItems;
            if (siblings == null) return;

            foreach (var otherMenuItem in siblings)
            {
                if (otherMenuItem.isDefault)
                {
                    Undo.RecordObject(otherMenuItem, "");
                    otherMenuItem.isDefault = false;
                    EditorUtility.SetDirty(otherMenuItem);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(otherMenuItem);
                }
            }
        }

        private void EnsureLabelCount(int i)
        {
            if (_labels == null || _labelsRoot.arraySize < i || _labels.Length < i)
            {
                _labelsRoot.arraySize = i;
                _labels = new SerializedProperty[i];
                for (int j = 0; j < i; j++)
                {
                    _labels[j] = _labelsRoot.GetArrayElementAtIndex(j);
                }
            }
        }

        private void CenterLabel(Rect rect, GUIContent content, GUIStyle style)
        {
            var size = style.CalcSize(content);
            var x = rect.x + rect.width / 2 - size.x / 2;
            var y = rect.y + rect.height / 2 - size.y / 2;
            GUI.Label(new Rect(x, y, size.x, size.y), content, style);
        }

        private void DoFourAxisLabels(bool showParams)
        {
            float maxWidth = 128 * 3;

            EnsureLabelCount(4);
            if (showParams) EnsureParameterCount(4);

            float extraHeight = EditorGUIUtility.singleLineHeight * 3;
            if (showParams) extraHeight += EditorGUIUtility.singleLineHeight;

            EditorGUILayout.LabelField(
                G(showParams ? "menuitem.label.control_labels_and_params" : "menuitem.label.control_labels"),
                EditorStyles.boldLabel);

            var square = GUILayoutUtility.GetAspectRect(1, GUILayout.MaxWidth(maxWidth));
            var extraSpace = GUILayoutUtility.GetRect(0, 0, extraHeight,
                extraHeight, GUILayout.ExpandWidth(true));

            var rect = square;
            rect.height += extraSpace.height;

            float extraWidth = Math.Max(0, extraSpace.width - rect.width);
            rect.x += extraWidth / 2;

            var blockHeight = rect.height / 3;
            var blockWidth = rect.width / 3;

            var up = rect;
            up.yMax -= blockHeight * 2;
            up.xMin += blockWidth;
            up.xMax -= blockWidth;

            var down = rect;
            down.yMin += blockHeight * 2;
            down.xMin += blockWidth;
            down.xMax -= blockWidth;

            var left = rect;
            left.yMin += blockHeight;
            left.yMax -= blockHeight;
            left.xMax -= blockWidth * 2;

            var right = rect;
            right.yMin += blockHeight;
            right.yMax -= blockHeight;
            right.xMin += blockWidth * 2;

            var center = rect;
            center.yMin += blockHeight;
            center.yMax -= blockHeight;
            center.xMin += blockWidth;
            center.xMax -= blockWidth;

            SingleLabel(0, up);
            SingleLabel(1, right);
            SingleLabel(2, down);
            SingleLabel(3, left);

            var rect_param_l = center;
            rect_param_l.yMin = rect_param_l.yMax - EditorGUIUtility.singleLineHeight;
            var rect_name_l = rect_param_l;
            if (showParams) rect_name_l.y -= rect_param_l.height;

            if (showParams) CenterLabel(rect_param_l, G("menuitem.prop.parameter"), EditorStyles.label);
            CenterLabel(rect_name_l, G("menuitem.prop.label"), EditorStyles.label);

            void SingleLabel(int index, Rect block)
            {
                var prop_name = _labels[index].FindPropertyRelative(nameof(VRCExpressionsMenu.Control.Label.name));
                var prop_icon = _labels[index].FindPropertyRelative(nameof(VRCExpressionsMenu.Control.Label.icon));

                var rect_param = block;
                rect_param.yMin = rect_param.yMax - EditorGUIUtility.singleLineHeight;

                var rect_name = rect_param;
                if (showParams) rect_name.y -= rect_param.height;

                var rect_icon = block;
                rect_icon.yMax = rect_name.yMin;

                EditorGUI.PropertyField(rect_name, prop_name, GUIContent.none);
                if (showParams)
                {
                    _subParams[index].DoGUI(rect_param, true, GUIContent.none);
                }

                var tex = prop_icon.objectReferenceValue as Texture;
                GUIContent icon_content;

                if (prop_icon.hasMultipleDifferentValues)
                {
                    icon_content = G("menuitem.misc.multiple");
                }
                else
                {
                    icon_content = tex != null ? new GUIContent(tex) : G("menuitem.misc.no_icon");
                }

                int objectId = GUIUtility.GetControlID(
                    ((int) IdGenerator.GetId(this, out bool _) << 2) | index,
                    FocusType.Passive,
                    block
                );

                if (GUI.Button(rect_icon, icon_content))
                {
                    texPicker = index;

                    EditorGUIUtility.ShowObjectPicker<Texture2D>(
                        prop_icon.hasMultipleDifferentValues ? null : prop_icon.objectReferenceValue, false,
                        "t:texture2d", objectId);
                }

                if (texPicker == index)
                {
                    if (Event.current.commandName == "ObjectSelectorUpdated" &&
                        EditorGUIUtility.GetObjectPickerControlID() == objectId)
                    {
                        prop_icon.objectReferenceValue = EditorGUIUtility.GetObjectPickerObject() as Texture;
                        _redraw();
                    }
                }
            }
        }

        private void EnsureParameterCount(int i)
        {
            if (_subParams == null || _subParamsRoot.arraySize < i || _subParams.Length < i)
            {
                _subParamsRoot.arraySize = i;
                _subParams = new ParameterGUI[i];
                for (int j = 0; j < i; j++)
                {
                    var prop = _subParamsRoot.GetArrayElementAtIndex(j)
                        .FindPropertyRelative(nameof(VRCExpressionsMenu.Control.Parameter.name));
                    _subParams[j] = new ParameterGUI(_parameterReference, prop, _redraw);
                }
            }
        }
    }
}

#endif