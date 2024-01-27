#if MA_VRCSDK3_AVATARS && UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static nadena.dev.modular_avatar.core.editor.Localization;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(ModularAvatarParameters))]
    internal class AvatarParametersEditor : MAEditorBase
    {        
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

        private ListView listView, unregisteredListView;

        private List<DetectedParameter> detectedParameters = new List<DetectedParameter>();
        
        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = uxml.CloneTree();
            UI.Localize(root);
            root.styleSheets.Add(uss);
            
            listView = root.Q<ListView>("Parameters");
            
            listView.showBoundCollectionSize = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            
            unregisteredListView = root.Q<ListView>("UnregisteredParameters");

            unregisteredListView.showBoundCollectionSize = false;
            unregisteredListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            unregisteredListView.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("DetectedParameter");

                return row;
            };
            unregisteredListView.bindItem = (elem, i) =>
            {
                var parameter = detectedParameters[i];
                elem.Clear();
                
                var button = new Button();
                button.text = "merge_parameter.ui.add_button";
                button.AddToClassList("ndmf-tr");
                UI.Localize(button);

                var label = new Label();
                label.text = parameter.OriginalName;
                elem.Add(button);
                elem.Add(label);

                if (parameter.Source != null)
                {
                    var tex = EditorGUIUtility.FindTexture("d_Search Icon");

                    var sourceButton = new Button();
                    sourceButton.AddToClassList("SourceButton");
                    sourceButton.text = "";

                    var image = new Image();
                    sourceButton.Add(image);
                    image.image = tex;
                    
                    sourceButton.clicked += () =>
                    {
                        EditorGUIUtility.PingObject(parameter.Source);
                    };
                    elem.Add(sourceButton);
                }

                button.clicked += () =>
                {
                    detectedParameters.RemoveAt(i);

                    var target = (ModularAvatarParameters)this.target;
                    target.parameters.Add(new ParameterConfig()
                    {
                        internalParameter = false,
                        nameOrPrefix = parameter.OriginalName,
                        isPrefix = parameter.IsPrefix,
                        remapTo = "",
                        syncType = parameter.syncType,
                        defaultValue = parameter.defaultValue,
                        saved = parameter.saved,
                    });
                    EditorUtility.SetDirty(target);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    
                    unregisteredListView.RefreshItems();
                    listView.RefreshItems();
                    listView.selectedIndex = target.parameters.Count - 1;
                };
            };

            unregisteredListView.itemsSource = detectedParameters;
            
            var unregisteredFoldout = root.Q<Foldout>("UnregisteredFoldout");
            unregisteredFoldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    DetectParameters();
                }
            });
            
            root.Bind(serializedObject);

            listView.itemsRemoved += _ =>
            {
                if (unregisteredFoldout.value)
                {
                    // We haven't committed the removal to the backing object yet, so defer this one frame to allow that
                    // to happen.
                    EditorApplication.delayCall += DetectParameters;
                }
            };
            
            return root;
        }

        private void DetectParameters()
        {
            var known = new HashSet<string>();
            var knownPB = new HashSet<string>();

            var target = (ModularAvatarParameters)this.target;
            foreach (var parameter in target.parameters)
            {
                if (parameter.isPrefix)
                {
                    knownPB.Add(parameter.nameOrPrefix);
                }
                else
                {
                    known.Add(parameter.nameOrPrefix);
                }
            }
                    
            var detected = ParameterPolicy.ProbeParameters(target.gameObject);
            detectedParameters.Clear();
            detectedParameters.AddRange(
                detected.Values
                    .Where(p =>
                        p.IsPrefix ? !knownPB.Contains(p.OriginalName) : !known.Contains(p.OriginalName))
                    .OrderBy(p => p.OriginalName)
            );
            unregisteredListView.RefreshItems();
        }
    }
}
#endif