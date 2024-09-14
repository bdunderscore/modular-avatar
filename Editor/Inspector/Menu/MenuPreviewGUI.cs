#if MA_VRCSDK3_AVATARS

using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.menu;
using static nadena.dev.modular_avatar.core.editor.Localization;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuObjectHeader
    {
        private const float INDENT_PER_LEVEL = 2;
        private static float _indentLevel = 0;

        private UnityEngine.Object _headerObj;
        private SerializedProperty _disableProp;

        public MenuObjectHeader(UnityEngine.Object headerObj, SerializedProperty disableProp = null)
        {
            _headerObj = headerObj;
            _disableProp = disableProp;
        }

        public static void ClearIndent()
        {
            _indentLevel = 0;
        }

        public IDisposable Scope()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_indentLevel);
            _indentLevel += INDENT_PER_LEVEL;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_headerObj != null)
            {
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                try
                {
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
                        GUILayout.Space(10);
                        GUILayout.Label("Enabled", GUILayout.Width(50));
                        EditorGUILayout.PropertyField(_disableProp, GUIContent.none,
                            GUILayout.Width(EditorGUIUtility.singleLineHeight));
                        _disableProp.serializedObject.ApplyModifiedProperties();
                    }

                    GUILayout.EndHorizontal();
                }
                finally
                {
                    EditorGUI.indentLevel = oldIndent;
                }
            }

            return new ScopeSentinel();
        }

        private class ScopeSentinel : IDisposable
        {
            public ScopeSentinel()
            {
            }

            public void Dispose()
            {
                GUILayout.EndVertical();
                _indentLevel -= INDENT_PER_LEVEL;
                GUILayout.EndHorizontal();
            }
        }
    }

    internal class MenuPreviewGUI
    {
        private Action _redraw;
        private readonly Dictionary<object, Action> _guiNodes = new Dictionary<object, Action>();

        public MenuPreviewGUI(Action redraw)
        {
            _redraw = redraw;
        }

        public void DoGUI(MenuSource root)
        {
            new VisitorContext(this).PushNode(root);
        }

        public void DoGUI(ModularAvatarMenuInstaller root)
        {
            new VisitorContext(this).PushMenuInstaller(root);
        }

        public void DoGUI(VRCExpressionsMenu menu, GameObject parameterReference = null)
        {
            new VisitorContext(this).PushMenuContents(menu);
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


        private class VisitorContext : NodeContext
        {
            private readonly HashSet<object> _visited = new HashSet<object>();
            private readonly MenuPreviewGUI _gui;

            public VisitorContext(MenuPreviewGUI gui)
            {
                _gui = gui;
            }

            public void PushMenu(VRCExpressionsMenu expMenu, GameObject parameterReference = null)
            {
                _gui.PushGuiNode((expMenu, parameterReference), () =>
                {
                    var header = new MenuObjectHeader(expMenu);
                    var obj = new SerializedObject(expMenu);
                    var controls = obj.FindProperty(nameof(expMenu.controls));
                    var subGui = new List<MenuItemCoreGUI>();
                    for (int i = 0; i < controls.arraySize; i++)
                    {
                        subGui.Add(new MenuItemCoreGUI(parameterReference, controls.GetArrayElementAtIndex(i),
                            _gui._redraw));
                    }

                    return () =>
                    {
                        using (header.Scope())
                        {
                            foreach (var gui in subGui)
                            {
                                using (new MenuObjectHeader(null).Scope())
                                {
                                    gui.DoGUI();
                                }
                            }
                        }
                    };
                });
            }

            public void PushMenuContents(VRCExpressionsMenu expMenu)
            {
                PushMenu(expMenu, null);
            }

            public void PushNode(MenuSource source)
            {
                var originalSource = source;
                if (source is ModularAvatarMenuGroup group)
                {
                    // Avoid calling source.Visit as this results in an extra MenuObjectHeader
                    // TODO: Avoid this unnecessary header in a cleaner way?
                    source = new MenuNodesUnder(group.targetObject != null ? group.targetObject : group.gameObject);
                }

                if (source is ModularAvatarMenuItem item)
                {
                    _gui.PushGuiNode(item, () =>
                    {
                        var header = new MenuObjectHeader(item,
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
                    using (new MenuObjectHeader(originalSource as UnityEngine.Object).Scope())
                    {
                        if (_visited.Contains(originalSource)) return;
                        _visited.Add(originalSource);

                        source.Visit(this);

                        if (source is MenuNodesUnder nodesUnder)
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(G("menuitem.misc.add_item")))
                            {
                                var newChild = new GameObject();
                                newChild.name = "New item";
                                newChild.transform.SetParent(nodesUnder.root.transform, false);

                                var mami = newChild.AddComponent<ModularAvatarMenuItem>();
                                mami.InitSettings();

                                Undo.RegisterCreatedObjectUndo(newChild, "Added menu item");
                            }

                            if (GUILayout.Button(G("menuitem.misc.add_toggle")))
                            {
                                var newChild = new GameObject();
                                newChild.name = "New toggle";
                                newChild.transform.SetParent(nodesUnder.root.transform, false);
                                
                                var mami = newChild.AddComponent<ModularAvatarMenuItem>();
                                mami.InitSettings();
                                mami.Control = new VRCExpressionsMenu.Control()
                                {
                                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                                    value = 1,
                                };

                                newChild.AddComponent<ModularAvatarObjectToggle>();

                                Selection.activeObject = newChild;
                                Undo.RegisterCreatedObjectUndo(newChild, "Added menu toggle");
                            }
                            
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }

            public void PushNode(ModularAvatarMenuInstaller installer)
            {
                using (new MenuObjectHeader(installer).Scope())
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
                    PushMenu(installer.menuToAppend, installer.gameObject);
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

#endif