#region

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ParamsUsageUI : VisualElement
    {
        private static readonly Type editorElem = AccessTools.TypeByName("UnityEditor.UIElements.EditorElement");
        private static readonly PropertyInfo editorElem_editor = AccessTools.Property(editorElem, "editor");
        
        private class FoldoutState
        {
            public bool Visible;
        }

        private static ConditionalWeakTable<VisualElement, FoldoutState> FoldoutStateHolder =
            new ConditionalWeakTable<VisualElement, FoldoutState>();

        private VisualElement _gameObjectEditorElement;
        private Editor _parentEditor;
        private Object _rawTarget;
        private GameObject _target;
        private ParamsUsageEditor _editor;
        private FoldoutState _foldoutState;

        private bool _recursing = false;

        public ParamsUsageUI()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);

            LanguagePrefs.RegisterLanguageChangeCallback(this,
                (self) => self.OnLanguageChangedCallback());
        }

        private void OnLanguageChangedCallback()
        {
            if (_editor != null)
            {
                BuildContent();
            }
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (_recursing) return;
            
            Clear();

            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            if (_recursing) return;

            Rebuild();
        }

        private void Rebuild()
        {
            if (parent == null) return;

            SetRedrawSensor();

            if (_gameObjectEditorElement?.parent != parent)
            {
                _gameObjectEditorElement = null;

                var kv = FindEditorElement();
                if (kv != null)
                {
                    var elem = kv.Value.Item1;
                    var index = kv.Value.Item2;

                    if (index != parent.Children().ToList().IndexOf(this))
                    {
                        _recursing = true;
                        var p = parent;
                        RemoveFromHierarchy();
                        p.Insert(index + 1, this);
                        _recursing = false;
                    }

                    _gameObjectEditorElement = elem;
                }
            }

            if (_gameObjectEditorElement == null) return;

            _parentEditor = editorElem_editor.GetValue(_gameObjectEditorElement) as Editor;
            if (_parentEditor == null) return;

            _rawTarget = _parentEditor.target;
            _target = _rawTarget as GameObject;

            if (_target == null) return;

            Clear();
            _redrawSensorActive = false;
            BuildContent();
        }

        private (VisualElement, int)? FindEditorElement()
        {
            foreach (var (elem, index) in parent.Children().Select((e, i) => (e, i)))
            {
                if (elem.ClassListContains("game-object-inspector"))
                {
                    return (elem, index);
                }
            }

            return null;
        }

        private bool _redrawSensorActive = false;

        private void SetRedrawSensor()
        {
            if (_redrawSensorActive) return;

            Clear();
            _redrawSensorActive = true;
            Add(new IMGUIContainer(() => EditorApplication.delayCall += Rebuild));
        }

        private void BuildContent()
        {
            Clear();

            if (!FoldoutStateHolder.TryGetValue(parent, out _foldoutState))
            {
                _foldoutState = new FoldoutState();
                FoldoutStateHolder.Add(parent, _foldoutState);
            }

            if (RuntimeUtil.FindAvatarTransformInParents(_target.transform) == null)
            {
                return;
            }

            _editor = Editor.CreateEditorWithContext(new Object[] { ModularAvatarInformation.instance }, _target,
                    typeof(ParamsUsageEditor))
                as ParamsUsageEditor;

            if (_editor == null) return;

            var inspectorElement = new InspectorElement(_editor);

            Add(new IMGUIContainer(() =>
            {
                if (_gameObjectEditorElement?.parent != parent || _parentEditor == null ||
                    _parentEditor.target != _rawTarget)
                {
                    EditorApplication.delayCall += Rebuild;
                    return;
                }
                
                switch (Event.current.rawType)
                {
                    case EventType.Repaint:
                    case EventType.MouseMove:
                    case EventType.Layout:
                        break;
                    case EventType.MouseDrag:
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                    case EventType.DragExited:
                        return;

                    default:
                        break;
                }

                _foldoutState.Visible = EditorGUILayout.InspectorTitlebar(_foldoutState.Visible, _editor);
                inspectorElement.style.display = _foldoutState.Visible ? DisplayStyle.Flex : DisplayStyle.None;
                _editor.Visible = _foldoutState.Visible;
            }));
            _editor.Visible = _foldoutState.Visible;
            Add(inspectorElement);
        }
    }
}