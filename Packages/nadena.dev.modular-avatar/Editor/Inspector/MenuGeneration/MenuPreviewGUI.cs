using System;
using System.Collections.Generic;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Collections;
using nadena.dev.modular_avatar.core.menu;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuPreviewGUI
    {
        private const float INDENT_PER_LEVEL = 2;
        private Action _redraw;
        private float _indentLevel = 0;
        private readonly Dictionary<object, Action> _guiNodes = new Dictionary<object, Action>();

        public MenuPreviewGUI(Action redraw)
        {
            _redraw = redraw;
        }

        public void DoGUI(MenuSource root)
        {
            _indentLevel = 0;
            new VisitorContext(this).PushNode(root);
        }

        public void DoGUI(ModularAvatarMenuInstaller root)
        {
            _indentLevel = 0;
            new VisitorContext(this).PushMenuInstaller(root);
        }

        private void PushGuiNode(object key, Func<Action> guiBuilder)
        {
            if (!_guiNodes.TryGetValue(key, out var gui))
            {
                gui = guiBuilder();
                _guiNodes.Add(key, gui);
            }

            gui();
        }

        private class Header
        {
            private MenuPreviewGUI _gui;
            private UnityEngine.Object _headerObj;
            private SerializedProperty _disableProp;

            public Header(MenuPreviewGUI gui, UnityEngine.Object headerObj, SerializedProperty disableProp = null)
            {
                _gui = gui;
                _headerObj = headerObj;
                _disableProp = disableProp;
            }

            public IDisposable Scope()
            {
                if (_headerObj == null) return new NullScope();

                GUILayout.BeginHorizontal();
                GUILayout.Space(_gui._indentLevel);
                _gui._indentLevel += INDENT_PER_LEVEL;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(new GUIContent(), _headerObj, _headerObj.GetType(),
                        true,
                        GUILayout.ExpandWidth(true));
                }

                if (_disableProp != null)
                {
                    _disableProp.serializedObject.Update();
                    GUILayout.Space(20);
                    GUILayout.Label("Enabled", GUILayout.Width(50));
                    EditorGUILayout.PropertyField(_disableProp, GUIContent.none,
                        GUILayout.Width(EditorGUIUtility.singleLineHeight));
                    _disableProp.serializedObject.ApplyModifiedProperties();
                }

                GUILayout.EndHorizontal();

                return new ScopeSentinel(_gui);
            }

            private class NullScope : IDisposable
            {
                public void Dispose()
                {
                }
            }

            private class ScopeSentinel : IDisposable
            {
                private readonly MenuPreviewGUI _gui;

                public ScopeSentinel(MenuPreviewGUI gui)
                {
                    _gui = gui;
                }

                public void Dispose()
                {
                    GUILayout.EndVertical();
                    _gui._indentLevel -= INDENT_PER_LEVEL;
                    GUILayout.EndHorizontal();
                }
            }
        }

        private class VisitorContext : NodeContext
        {
            private readonly HashSet<object> _visited = new HashSet<object>();
            private readonly MenuPreviewGUI _gui;

            public VisitorContext(MenuPreviewGUI gui)
            {
                _gui = gui;
            }

            public void PushNode(VRCExpressionsMenu expMenu)
            {
                foreach (var control in expMenu.controls)
                {
                    PushControl(control);
                }
            }

            public void PushNode(MenuSource source)
            {
                if (source is ModularAvatarMenuItem item)
                {
                    _gui.PushGuiNode(item, () =>
                    {
                        var header = new Header(_gui, item,
                            new SerializedObject(item.gameObject).FindProperty("m_IsActive"));
                        var gui = new MenuItemCoreGUI(new SerializedObject(item), _gui._redraw);
                        return () =>
                        {
                            using (header.Scope())
                            {
                                gui.DoGUI();
                            }
                        };
                    });
                }
                else
                {
                    using (new Header(_gui, source as UnityEngine.Object).Scope())
                    {
                        if (_visited.Contains(source)) return;
                        _visited.Add(source);

                        source.Visit(this);
                    }
                }
            }

            public void PushNode(ModularAvatarMenuInstaller installer)
            {
                using (new Header(_gui, installer).Scope())
                {
                    PushMenuInstaller(installer);
                }
            }

            internal void PushMenuInstaller(ModularAvatarMenuInstaller installer)
            {
                var source = installer.GetComponent<MenuSource>();
                if (source != null)
                {
                    PushNode(source);
                }
                else if (installer.menuToAppend != null)
                {
                    PushNode(installer.menuToAppend);
                }
            }

            public void PushControl(VRCExpressionsMenu.Control control)
            {
                // Construct a read-only GUI, as we can't build a serialized property reference for this control object
                _gui.PushGuiNode(control, () =>
                {
                    var container = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    container.controls = new List<VRCExpressionsMenu.Control> {control};
                    var prop = new SerializedObject(container).FindProperty("controls").GetArrayElementAtIndex(0);
                    var gui = new MenuItemCoreGUI(null, prop, _gui._redraw);
                    return () =>
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            gui.DoGUI();
                        }
                    };
                });
            }

            public void PushControl(VirtualControl control)
            {
                PushControl((VRCExpressionsMenu.Control) control);
            }

            public VirtualMenuNode NodeFor(VRCExpressionsMenu menu)
            {
                return new VirtualMenuNode(menu);
            }

            public VirtualMenuNode NodeFor(MenuSource menu)
            {
                return new VirtualMenuNode(menu);
            }
        }
    }
}