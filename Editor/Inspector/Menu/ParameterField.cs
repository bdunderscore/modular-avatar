#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ParameterGUI
    {
        private GameObject _parameterReference;
        private readonly SerializedProperty _property;
        private readonly Action _redraw;
        private Rect fieldRect;

        internal ParameterGUI(GameObject parameterReference, SerializedProperty property, Action redraw)
        {
            _parameterReference = parameterReference;
            _property = property;
            _redraw = redraw;
        }

        public void DoGUI(bool enabled, GUIContent label = null)
        {
            DoGUI(EditorGUILayout.GetControlRect(
                true,
                EditorGUIUtility.singleLineHeight
            ), enabled, label);
        }

        public void DoGUI(Rect rect, bool enabled, GUIContent label = null)
        {
            label = label ?? G("menuitem.prop.parameter");

            if (!enabled)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(rect, label, S("menuitem.param.controlled_by_action"));
                    return;
                }
            }

            if (_parameterReference != null) GUILayout.Space(-2);
            GUILayout.BeginHorizontal();

            rect.width -= EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(rect, _property, label);
            Rect propField = new Rect();

            if (Event.current.type == EventType.Repaint)
            {
                propField = rect;
            }

            Rect buttonRect = rect;
            buttonRect.xMin = rect.xMax;
            buttonRect.width = EditorGUIUtility.singleLineHeight;

            GUIStyle style = "IN DropDown";

            buttonRect.xMin += (buttonRect.width - style.fixedWidth) / 2;

            if (_parameterReference != null &&
                EditorGUI.DropdownButton(buttonRect, new GUIContent(), FocusType.Keyboard, style))
            {
                PopupWindow.Show(fieldRect,
                    new ParameterWindow(_parameterReference, _property, fieldRect.width, _redraw));
            }

            if (Event.current.type == EventType.Repaint)
            {
                float labelWidth = label == GUIContent.none ? 0 : EditorGUIUtility.labelWidth;

                fieldRect = propField;
                fieldRect.x += labelWidth + 2;
                fieldRect.width = buttonRect.xMax - propField.x - labelWidth;
                fieldRect.height = 0;
            }

            GUILayout.EndHorizontal();
            if (_parameterReference != null) GUILayout.Space(2);
        }

        private class ParameterWindow : PopupWindowContent
        {
            private readonly GameObject _target;
            private readonly SerializedProperty _prop;
            private readonly float _width;
            private readonly Action _redraw;

            private SearchField _searchField;
            private ParameterTree _tree;
            private string _searchString;

            public ParameterWindow(GameObject target, SerializedProperty prop, float width, Action redraw)
            {
                _target = target;
                _prop = prop;
                _width = width;
                _redraw = redraw;
            }

            public override void OnGUI(Rect rect)
            {
                var sfRect = rect;
                sfRect.height = EditorGUIUtility.singleLineHeight;
                rect.y += EditorGUIUtility.singleLineHeight;
                rect.height -= EditorGUIUtility.singleLineHeight;

                if (_searchField == null)
                {
                    _searchField = new SearchField();
                }

                _searchString = _searchField.OnGUI(sfRect, _searchString);

                if (_tree == null)
                {
                    _tree = new ParameterTree(new TreeViewState(), _target);
                    _tree.OnSelect = (s) =>
                    {
                        _prop.stringValue = s;
                        _prop.serializedObject.ApplyModifiedProperties();
                        _redraw();
                    };

                    _tree.OnCommit = (s) =>
                    {
                        _prop.stringValue = s;
                        _prop.serializedObject.ApplyModifiedProperties();
                        editorWindow.Close();
                        _redraw();
                    };
                    _tree.Reload();
                }

                _tree.searchString = _searchString;
                _tree.OnGUI(rect);
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(Math.Max(256, _width), 150);
            }
        }

        private class ParameterTree : TreeView
        {
            private List<string> _items;
            public Action<string> OnSelect, OnCommit;

            private GameObject _obj;

            private class SourceItem : TreeViewItem
            {
                public GameObject source;
            }

            private class ParamItem : TreeViewItem
            {
                public GameObject source;
            }

            public ParameterTree(TreeViewState state, GameObject obj) : base(state)
            {
                _obj = obj;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                var item = _items[selectedIds[0]];
                if (item != null) OnSelect(item);
            }

            protected override void DoubleClickedItem(int id)
            {
                var item = _items[id];
                if (item != null) OnCommit(item);
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (!string.IsNullOrEmpty(searchString) && args.item is ParamItem offer)
                {
                    var rect = args.rowRect;

                    var objName = offer.source.name + " / ";
                    var content = new GUIContent(objName);

                    var width = EditorStyles.label.CalcSize(content).x;
                    var color = GUI.color;

                    var grey = color;
                    grey.a *= 0.7f;
                    GUI.color = grey;

                    EditorGUI.LabelField(rect, content);

                    GUI.color = color;

                    rect.x += width;
                    rect.width -= width;

                    if (rect.width >= 0)
                    {
                        EditorGUI.LabelField(rect, offer.displayName);
                    }
                }
                else if (args.item is SourceItem source)
                {
                    var rect = args.rowRect;
                    rect.xMin += this.GetContentIndent(args.item) + this.extraSpaceBeforeIconAndLabel;
                    EditorGUI.LabelField(rect, source.source.name);
                }
                else
                {
                    base.RowGUI(args);
                }
            }

            protected override TreeViewItem BuildRoot()
            {
                List<TreeViewItem> treeItems = new List<TreeViewItem>();
                _items = new List<string>();

                _items.Add("");
                var root = new TreeViewItem {id = 0, depth = -1, displayName = "Root"};

                GameObject priorNode = null;

                foreach ((GameObject node, string param) in FindParameters())
                {
                    if (node != priorNode)
                    {
                        _items.Add(null);
                        treeItems.Add(new SourceItem()
                            {id = _items.Count - 1, depth = 0, displayName = "", source = node});
                        priorNode = node;
                    }

                    _items.Add(param);
                    treeItems.Add(new ParamItem {id = _items.Count - 1, depth = 1, displayName = param, source = node});
                }

                SetupParentsAndChildrenFromDepths(root, treeItems);

                return root;
            }

            private IEnumerable<(GameObject, string)> FindParameters()
            {
                HashSet<string> emitted = new HashSet<string>();
                GameObject node = _obj;
                while (node != null && node.GetComponent<VRCAvatarDescriptor>() == null)
                {
                    var paramComp = node.GetComponent<ModularAvatarParameters>();
                    if (paramComp != null)
                    {
                        foreach (var param in paramComp.parameters)
                        {
                            if (!param.isPrefix)
                            {
                                if (emitted.Add(param.nameOrPrefix)) yield return (node, param.nameOrPrefix);
                            }
                        }
                    }

                    node = node.transform.parent?.gameObject;
                }

                var desc = node?.GetComponent<VRCAvatarDescriptor>();
                if (desc?.expressionParameters?.parameters != null)
                {
                    foreach (var param in desc.expressionParameters.parameters)
                    {
                        if (param == null) continue;
                        if (emitted.Add(param.name)) yield return (node, param.name);
                    }
                }
            }
        }
    }
}

#endif