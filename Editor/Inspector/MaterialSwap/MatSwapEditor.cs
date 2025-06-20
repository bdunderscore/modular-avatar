#nullable enable

#region

using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.modular_avatar.core.editor.ShapeChanger
{
    [CustomPropertyDrawer(typeof(MatSwap))]
    internal class MatSwapEditor : VisualElement
    {
        private const string Root = "Packages/nadena.dev.modular-avatar/Editor/Inspector/MaterialSwap/";
        private const string UxmlPath = Root + "MatSwapEditor.uxml";
        private const string UssPath = Root + "MaterialSwapStyles.uss";

        private bool _isAttached;
        private IDisposable? _deregisterFrom, _deregisterTo;
        private MaterialSwapEditor _parentEditor;

        private PropertyField _fromField, _toField;
        
        private Label? toPathLabel;
        private Label? fromPathLabel;

        private SerializedProperty? _property;
        private TemplateContainer _uxml;

        public MatSwapEditor(MaterialSwapEditor parentEditor)
        {
            _parentEditor = parentEditor;
            
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).CloneTree();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            _uxml = uxml;
            Add(uxml);

            Localization.UI.Localize(uxml);
            uxml.styleSheets.Add(uss);
            
            uxml.RegisterCallback<AttachToPanelEvent>(OnAttach);
            uxml.RegisterCallback<DetachFromPanelEvent>(OnDetach);

            _fromField = uxml.Q<PropertyField>("from-field");
            _toField = uxml.Q<PropertyField>("to-field");
            
            _fromField.RegisterValueChangeCallback(OnAnyValueChanged);
            _toField.RegisterValueChangeCallback(OnAnyValueChanged);
            
            var fromSelector = this.Q<Button>("from-selector");
            fromSelector.clicked += () =>
            {
                if (_property == null) return;
                
                var fromProperty = _property.FindPropertyRelative("From");
                var toProperty = _property.FindPropertyRelative("To");
                var swap = _property.serializedObject.targetObject as ModularAvatarMaterialSwap;
                if (swap == null)
                {
                    return;
                }
                var root = swap.Root.Get(swap)?.transform ?? RuntimeUtil.FindAvatarTransformInParents(swap.transform);
                if (root == null)
                {
                    return;
                }

                var menu = new GenericDropdownMenu();
                foreach (var material in root.GetComponentsInChildren<Renderer>(true)
                             .SelectMany(x => x.sharedMaterials)
                             .Where(x => x != null)
                             .Distinct())
                {
                    menu.AddItem(material.name, material == fromProperty.objectReferenceValue, () =>
                    {
                        fromProperty.serializedObject.Update();
                        fromProperty.objectReferenceValue = material;
                        if (toProperty.objectReferenceValue == null)
                        {
                            // Avoid turning things purple when we first start setting things up.
                            toProperty.objectReferenceValue = material;
                        }
                        fromProperty.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.DropDown(_fromField.worldBound, _fromField);
            };
        }
        
        // We should probably support IBindable, but the documentation is seemingly nonexistent
        public new void BindProperty(SerializedProperty property)
        {
            _property = property;
            _uxml.BindProperty(property);
        }

        private void RegisterUniquePathCallbacks()
        {
            if (_parentEditor == null) return;
            
            var fromProperty = _property?.FindPropertyRelative("From");
            var toProperty = _property?.FindPropertyRelative("To");

            if (fromProperty == null || toProperty == null) return;

            RegisterUniquePathCallbackForProperty(fromProperty, ref _deregisterFrom, _fromField, ref fromPathLabel);
            RegisterUniquePathCallbackForProperty(toProperty, ref _deregisterTo, _toField, ref toPathLabel);
        }

        private void RegisterUniquePathCallbackForProperty(SerializedProperty prop, ref IDisposable? deregister, PropertyField field, ref Label? pathLabel)
        {
            deregister?.Dispose();
            deregister = null;
            
            // The PropertyField creates its inner label after a delay, so look for it in this delayed RegisterUniquePathCallbacks
            // callback.
            if (pathLabel == null)
            {
                var primaryLabel = field.Q<Label>();
                if (primaryLabel == null) return;

                pathLabel = new Label();
                pathLabel.pickingMode = PickingMode.Ignore;
                pathLabel.AddToClassList("path-label");
                primaryLabel.parent.Insert(primaryLabel.parent.IndexOf(primaryLabel), pathLabel);
            }

            pathLabel.style.display = DisplayStyle.None;
            
            // Ignore if the object is not an asset
            var obj = prop.objectReferenceValue;
            if (obj == null || !AssetDatabase.Contains(obj)) return;

            var pathLabel_ = pathLabel; // preserve the value as we can't carry a ref into the callback
            deregister = _parentEditor?.RegisterUniquePathCallback(obj.name, obj, path =>
            {
                pathLabel_.text = path;
                pathLabel_.style.display = path == null ? DisplayStyle.None : DisplayStyle.Flex;
            });
        }

        private void OnAnyValueChanged(SerializedPropertyChangeEvent evt)
        {
            RegisterUniquePathCallbacks();
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            _deregisterFrom?.Dispose();
            _deregisterTo?.Dispose();
            _deregisterFrom = null;
            _deregisterTo = null;
            
            _isAttached = false;
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            RegisterUniquePathCallbacks();
        }
    }
}
