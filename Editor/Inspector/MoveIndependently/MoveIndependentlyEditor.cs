using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.ArmatureAwase;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomEditor(typeof(MAMoveIndependently))]
    internal class MoveIndependentlyEditor : MAEditorBase
    {
        [SerializeField] private StyleSheet uss;
        [SerializeField] private VisualTreeAsset uxml;

        private ComputeContext _ctx;
        private VisualElement _root;
        
        private TransformChildrenNode _groupedNodesElem;

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
            _root.Clear();
            _ctx = new ComputeContext("MoveIndependentlyEditor");
            _root.Add(BuildInnerGUI(_ctx));
        }

        private VisualElement BuildInnerGUI(ComputeContext ctx)
        {
            if (this.target == null) return new VisualElement();

            _ctx.InvokeOnInvalidate(this, editor => editor.RebuildInnerGUI());
            
#pragma warning disable CS0618 // Type or member is obsolete
            var root = uxml.Localize();
#pragma warning restore CS0618 // Type or member is obsolete
            root.styleSheets.Add(uss);

            var container = root.Q<VisualElement>("group-container");

            MAMoveIndependently target = (MAMoveIndependently) this.target;
            // Note: We specifically _don't_ use an ImmutableHashSet here as we want to update the previously-returned
            // set in place to avoid rebuilding GUI elements after the user changes the grouping.
            var grouped = ctx.Observe(target,
                t => (t.GroupedBones ?? Array.Empty<GameObject>())
                    .Select(obj => obj.transform)
                    .ToHashSet(),
                (x, y) => x.SetEquals(y)
            );

            _groupedNodesElem = new TransformChildrenNode(target.transform, grouped);
            _groupedNodesElem.AddToClassList("group-root");
            container.Add(_groupedNodesElem);
            _groupedNodesElem.OnChanged += () =>
            {
                Undo.RecordObject(target, "Toggle grouped nodes");
                target.GroupedBones = _groupedNodesElem.Active().Select(t => t.gameObject).ToArray();
                grouped.Clear();
                grouped.UnionWith(target.GroupedBones.Select(obj => obj.transform));
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            };

            return root;
        }

        private class TransformChildrenNode : VisualElement
        {
            private readonly Transform _transform;
            private HashSet<TransformChildrenNode> _active = new HashSet<TransformChildrenNode>();

            public Transform Transform => _transform;

            public event Action OnChanged;

            public IEnumerable<Transform> Active()
            {
                foreach (var child in _active)
                {
                    yield return child.Transform;
                    foreach (var subChild in child.Active())
                    {
                        yield return subChild;
                    }
                }
            }

            internal TransformChildrenNode(Transform transform, ICollection<Transform> enabled)
            {
                _transform = transform;

                foreach (Transform child in transform)
                {
                    var childRoot = new VisualElement();
                    Add(childRoot);

                    var toggleContainer = new VisualElement();
                    childRoot.Add(toggleContainer);
                    toggleContainer.AddToClassList("left-toggle");
                    var toggle = new Toggle();
                    toggleContainer.Add(toggle);
                    toggleContainer.Add(new Label(child.gameObject.name));

                    var childGroup = new VisualElement();
                    childRoot.Add(toggleContainer);
                    childRoot.Add(childGroup);

                    childGroup.AddToClassList("group-children");

                    TransformChildrenNode childNode = null;
                    Action<bool> setNodeState = newValue =>
                    {
                        if (childNode != null == newValue) return;

                        if (newValue)
                        {
                            childNode = new TransformChildrenNode(child, enabled);
                            _active.Add(childNode);
                            childNode.OnChanged += FireOnChanged;
                            childGroup.Add(childNode);
                        }
                        else
                        {
                            childGroup.Clear();
                            _active.Remove(childNode);
                            childNode = null;
                        }

                        FireOnChanged();
                    };

                    toggle.RegisterValueChangedCallback(ev => setNodeState(ev.newValue));
                    toggle.value = enabled.Contains(child);
                    setNodeState(toggle.value);
                }

                enabled = ImmutableHashSet<Transform>.Empty;
            }

            private void FireOnChanged()
            {
                OnChanged?.Invoke();
            }
        }
    }
}