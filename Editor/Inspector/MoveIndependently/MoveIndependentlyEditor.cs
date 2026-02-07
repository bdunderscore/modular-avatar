#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(MAMoveIndependently))]
    internal class MoveIndependentlyEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet? uss;
        [SerializeField] private VisualTreeAsset? uxml;

        private ComputeContext? _ctx;
        private VisualElement? _root;

        protected override void OnInnerInspectorGUI()
        {
            EditorGUILayout.HelpBox("Unable to show override changes", MessageType.Info);
        }

        protected override VisualElement CreateInnerInspectorGUI()
        {
            _root = new VisualElement();

            RebuildInnerGUI();

            return _root;
        }

        private void RebuildInnerGUI()
        {
            if (uss == null || uxml == null || _root == null) return;

            _root.Clear();
            _ctx = new ComputeContext("MoveIndependentlyEditor");
            _root.Add(BuildInnerGUI(_ctx));
        }

        private VisualElement BuildInnerGUI(ComputeContext ctx)
        {
            if (this.target == null) return new VisualElement();

            _ctx?.InvokeOnInvalidate(this, editor => editor.RebuildInnerGUI());
            _ctx?.GetComponentsInChildren<Transform>(((Component)this.target).gameObject, true);
            
#pragma warning disable CS0618 // Type or member is obsolete
            var root = uxml.Localize();
#pragma warning restore CS0618 // Type or member is obsolete
            root.styleSheets.Add(uss);

            var container = root.Q<VisualElement>("group-container");

            // ReSharper disable once LocalVariableHidesMember
            MAMoveIndependently target = (MAMoveIndependently) this.target;
            // Note: We specifically _don't_ use an ImmutableHashSet here as we want to update the previously-returned
            // set in place to avoid rebuilding GUI elements after the user changes the grouping.
            var grouped = ctx.Observe(target,
                t => (t.GroupedBones ?? Array.Empty<GameObject>())
                    .Select(obj => obj.transform)
                    .ToHashSet(),
                (x, y) => x.SetEquals(y)
            );

            Action refresh = () =>
            {
                Undo.RecordObject(target, "Toggle grouped nodes");
                target.GroupedBones = grouped.Where(t => t != null).Select(t => t.gameObject).ToArray();
                grouped.Clear();
                grouped.UnionWith(target.GroupedBones.Select(obj => obj.transform));
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            };

            void OnChange()
            {
                DeferRefresh.Invoke(int.MaxValue, this, refresh);
            }

            var groupedNodesElem = BuildTree(target.transform, OnChange, grouped);

            groupedNodesElem.AddToClassList("group-root");
            container.Add(groupedNodesElem);

            return root;
        }

        private TreeView BuildTree(Transform targetTransform, Action onChange, HashSet<Transform> grouped)
        {
            var treeView = new TreeView();

            var allItems = new List<BoneInfo>();
            var rootItems = new List<TreeViewItemData<BoneInfo>>();

            foreach (Transform rootChild in targetTransform)
            {
                rootItems.Add(VisitNode(rootChild, null));
            }

            treeView.SetRootItems(rootItems);
            treeView.makeItem = MakeItem;
            treeView.bindItem = BindItem;
            treeView.selectionType = SelectionType.None;
            treeView.Rebuild();

            return treeView;

            VisualElement MakeItem()
            {
                return new ToggleElement();
            }

            void BindItem(VisualElement element, int index)
            {
                var info = treeView.GetItemDataForIndex<BoneInfo>(index);

                if (element is ToggleElement te)
                {
                    te.BindTo(info);
                }
            }

            TreeViewItemData<BoneInfo> VisitNode(Transform t, BoneInfo? parent)
            {
                var itemIndex = allItems.Count;
                var boneInfo = new BoneInfo(t, onChange, grouped, parent);
                allItems.Add(boneInfo);

                if (parent != null)
                {
                    parent.Children.Add(boneInfo);
                }

                var children = new List<TreeViewItemData<BoneInfo>>();

                if (!boneInfo.Blocked)
                {
                    foreach (Transform child in t)
                    {
                        children.Add(VisitNode(child, boneInfo));
                    }
                }

                return new TreeViewItemData<BoneInfo>(itemIndex, boneInfo, children);
            }
        }

        private class ToggleElement : VisualElement
        {
            private readonly Toggle _toggle;
            private readonly Label _label;

            private BoneInfo? _boneInfo;

            public ToggleElement()
            {
                _toggle = new Toggle();
                _toggle.RegisterValueChangedCallback(OnToggleChanged);

                _label = new Label();

                Add(_toggle);
                Add(_label);
                AddToClassList("left-toggle");

                RegisterCallback<MouseDownEvent>(evt => { evt.StopPropagation(); });
                RegisterCallback<MouseUpEvent>(evt => { evt.StopPropagation(); });
            }

            internal void BindTo(BoneInfo boneInfo)
            {
                if (_boneInfo == boneInfo) return;
                if (_boneInfo != null)
                {
                    _boneInfo.Toggle = null;
                }

                _boneInfo = boneInfo;
                boneInfo.Toggle = this;
                _label.text = boneInfo.Transform.gameObject.name;
                Update();
            }

            private void OnToggleChanged(ChangeEvent<bool> evt)
            {
                if (_boneInfo == null) return;

                using var scope = DeferRefresh.Suppress();

                _boneInfo.SetActiveRecursive(evt.newValue);
            }

            public void Update()
            {
                if (_boneInfo == null) return;

                _toggle.showMixedValue = _boneInfo.Mixed;
                _toggle.SetValueWithoutNotify(_boneInfo.Active);
                var parentEnabled = _boneInfo.Parent?.Active ?? true;
                _toggle.SetEnabled(!_boneInfo.Blocked && parentEnabled);
            }
        }

        private class BoneInfo
        {
            private readonly HashSet<Transform> _grouped;
            private readonly Action _onChange;
            private readonly int _depth;

            public readonly BoneInfo? Parent;
            public readonly Transform Transform;
            public readonly bool Blocked;

            public ToggleElement? Toggle;

            public readonly List<BoneInfo> Children = new();

            public bool Active
            {
                get => _grouped.Contains(Transform) && !Blocked;
                private set
                {
                    if (value == Active) return;
                    if (Blocked) return;
                    if (value) _grouped.Add(Transform);
                    else _grouped.Remove(Transform);

                    _onChange();

                    Update();
                }
            }

            public bool Mixed => Active && Children.Any(x => x.Mixed || !x.Active);

            public BoneInfo(Transform t, Action onChange, HashSet<Transform> grouped, BoneInfo? parent)
            {
                Transform = t;
                _onChange = onChange;
                _grouped = grouped;
                Parent = parent;
                _depth = (parent?._depth ?? -1) + 1;
                Blocked = Transform.TryGetComponent<MAMoveIndependently>(out _);
            }

            public override string ToString()
            {
                return Transform.gameObject.name;
            }

            private void Update()
            {
                // Update from leaf to root to ensure the mixed values are set properly
                DeferRefresh.Invoke(-_depth, this, UpdateDeferred);
            }

            private void UpdateDeferred()
            {
                Parent?.Update();
                Toggle?.Update();
            }

            public void SetActiveRecursive(bool value)
            {
                if (Blocked) return;

                Active = value;
                foreach (var child in Children)
                {
                    child.SetActiveRecursive(value);
                    child.Update();
                }
            }
        }
    }
}