#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class BlendshapeSelectWindow : EditorWindow
    {
        internal GameObject? AvatarRoot;
        internal Mesh? SingleMesh;
        private BlendshapeTree? _tree;

        internal SearchField? _searchField;
        internal Action<BlendshapeBinding>? OfferBinding;
        internal Action<BlendshapeBinding>? OfferSingleClick;
        internal Action<IList<BlendshapeBinding>>? OfferMultipleBindings;

        private void Awake()
        {
            titleContent = new GUIContent("Select blendshapes");
        }
        
        private void OnLostFocus() 
        {
            Close();
        }
        
        void OnGUI()
        {
            var rect = new Rect(0, 0, position.width, position.height);

            if (_tree == null)
            {
                _searchField = new SearchField();
                if (SingleMesh != null)
                {
                    _tree = new BlendshapeTree(SingleMesh, new TreeViewState());
                } else if (AvatarRoot != null) {
                    _tree = new BlendshapeTree(AvatarRoot, new TreeViewState());
                }
                else
                {
                    Close();
                }
                _tree.OfferBinding = (binding) => OfferBinding?.Invoke(binding);
                _tree.OfferSingleClick = binding => OfferSingleClick?.Invoke(binding);
                _tree.OfferMultipleBindings = bindings => OfferMultipleBindings?.Invoke(bindings);
                _tree.Reload();

                _tree.SetExpanded(0, true);
            }

            var sfRect = GUILayoutUtility.GetRect(1, 99999, EditorGUIUtility.singleLineHeight,
                EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            _tree.searchString = _searchField!.OnGUI(sfRect, _tree.searchString);

            var remaining = GUILayoutUtility.GetRect(1, 99999, EditorGUIUtility.singleLineHeight * 2, 99999999,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _tree.OnGUI(remaining);

            var selectedCount = _tree.GetSelectedBindings().Count;
            using (new EditorGUI.DisabledScope(selectedCount < 2))
            {
                if (GUILayout.Button(Localization.S_f("blendshape.add_selected", selectedCount.ToString())))
                {
                    var bindings = _tree.GetSelectedBindings();
                    if (bindings.Count > 0)
                    {
                        OfferMultipleBindings?.Invoke(bindings);
                    }
                }
            }
        }
    }

    internal class BlendshapeTree : TreeView
    {
        internal class OfferItem : TreeViewItem
        {
            // Initialized when the item is created in CreateBlendshapes()
            public BlendshapeBinding binding = default!;
        }

        private readonly Mesh? _singleMesh;
        private readonly GameObject? _avatarRoot;
        // Initialized in BuildRoot() which is called by Unity TreeView before any access
        private List<BlendshapeBinding?> _candidateBindings = null!;

        // Set by caller after construction (see BlendshapeSelectWindow.OnGUI)
        internal Action<BlendshapeBinding> OfferBinding = null!;
        internal Action<BlendshapeBinding>? OfferSingleClick;
        internal Action<IList<BlendshapeBinding>>? OfferMultipleBindings;

        public BlendshapeTree(GameObject avatarRoot, TreeViewState state) : base(state)
        {
            this._avatarRoot = avatarRoot;
        }
        
        public BlendshapeTree(Mesh mesh, TreeViewState state) : base(state)
        {
            this._singleMesh = mesh;
        }

        public BlendshapeTree(GameObject avatarRoot, TreeViewState state, MultiColumnHeader multiColumnHeader) : base(
            state, multiColumnHeader)
        {
            this._avatarRoot = avatarRoot;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (!string.IsNullOrEmpty(searchString) && args.item is OfferItem offer)
            {
                var rect = args.rowRect;

                var binding = offer.binding;
                string objName;
                if (binding.ReferenceMesh == null)
                {
                    objName = "";
                }
                else
                {
                    objName = binding.ReferenceMesh.referencePath;
                    var index = objName.LastIndexOf('/');
                    if (index >= 0) objName = objName.Substring(index + 1);

                    objName += " / ";
                }

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
                    EditorGUI.LabelField(rect, binding.Blendshape);
                }
            }
            else
            {
                base.RowGUI(args);
            }
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return item is OfferItem;
        }

        internal IList<BlendshapeBinding> GetSelectedBindings()
        {
            var bindings = new List<BlendshapeBinding>();
            foreach (var id in GetSelection())
            {
                if (id >= 0 && id < _candidateBindings.Count && _candidateBindings[id].HasValue)
                {
                    bindings.Add(_candidateBindings[id]!.Value);
                }
            }
            return bindings;
        }

        protected override void KeyEvent()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                var bindings = GetSelectedBindings();
                if (bindings.Count > 0)
                {
                    OfferMultipleBindings?.Invoke(bindings);
                    Event.current.Use();
                }
            }
        }

        protected override void SingleClickedItem(int id)
        {
            var binding = _candidateBindings[id];
            if (binding.HasValue)
            {
                OfferSingleClick?.Invoke(binding.Value);
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            var binding = _candidateBindings[id];
            if (binding.HasValue)
            {
                OfferBinding.Invoke(binding.Value);
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem {id = 0, depth = -1, displayName = "Root"};
            _candidateBindings = new List<BlendshapeBinding?>();
            _candidateBindings.Add(null);

            var allItems = new List<TreeViewItem>();

            int createdDepth = 0;
            List<string> ObjectDisplayNames = new List<string>();

            if (_avatarRoot != null)
            {
                WalkTree(_avatarRoot, allItems, ObjectDisplayNames, ref createdDepth);
            }
            else
            {
                CreateBlendshapes(allItems, _singleMesh!, null, 0);
            }

            SetupParentsAndChildrenFromDepths(root, allItems);

            return root;
        }

        private void WalkTree(GameObject node, List<TreeViewItem> items, List<string> objectDisplayNames,
            ref int createdDepth)
        {
            objectDisplayNames.Add(node.name);

            var smr = node.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                while (createdDepth < objectDisplayNames.Count)
                {
                    items.Add(new TreeViewItem
                    {
                        id = _candidateBindings.Count, depth = createdDepth,
                        displayName = objectDisplayNames[createdDepth]
                    });
                    _candidateBindings.Add(null);
                    createdDepth++;
                }

                CreateBlendshapes(smr, items, ref createdDepth);
            }

            foreach (Transform child in node.transform)
            {
                WalkTree(child.gameObject, items, objectDisplayNames, ref createdDepth);
            }

            objectDisplayNames.RemoveAt(objectDisplayNames.Count - 1);
            createdDepth = Math.Min(createdDepth, objectDisplayNames.Count);
        }

        private void CreateBlendshapes(SkinnedMeshRenderer smr, List<TreeViewItem> items, ref int createdDepth)
        {
            items.Add(new TreeViewItem
                {id = _candidateBindings.Count, depth = createdDepth, displayName = "BlendShapes"});
            _candidateBindings.Add(null);
            createdDepth++;

            var path = RuntimeUtil.RelativePath(_avatarRoot, smr.gameObject);
            var mesh = smr.sharedMesh;
            
            CreateBlendshapes(items, mesh, path, createdDepth);

            createdDepth--;
        }

        private void CreateBlendshapes(List<TreeViewItem> items, Mesh mesh, string? path, int createdDepth)
        {
            List<BlendshapeBinding> bindings = Enumerable.Range(0, mesh.blendShapeCount)
                .Select(n =>
                {
                    var name = mesh.GetBlendShapeName(n);
                    return new BlendshapeBinding()
                    {
                        Blendshape = name,
                        ReferenceMesh = path == null ? null : new AvatarObjectReference()
                        {
                            referencePath = path
                        }
                    };
                })
                .ToList();

            foreach (var binding in bindings)
            {
                items.Add(new OfferItem
                {
                    id = _candidateBindings.Count, depth = createdDepth, displayName = binding.Blendshape,
                    binding = binding
                });
                _candidateBindings.Add(binding);
            }
        }
    }
}