#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal sealed partial class UnityBlendTreeBackend : IReactionBackend
    {
        public const string ALWAYS_ONE = "$$MA/RC/AlwaysOne";
        public AnimationIndex AnimationIndex { get; private set; }
        public ObjectPathRemapper ObjectPathRemapper { get; private set; }
        public VirtualMotion EmptyMotion { get; private set; }
        public VirtualClip AlwaysOnClip { get; }
        public VirtualBlendTree RootTree { get; }
        public VirtualBlendTree BaseLayerTree { get; }
        public VirtualClip BaseLayerClip { get; }
        private readonly VirtualAnimatorController _vac;
        private readonly ReactionParameters _fallbackParameters = new();
        private ReactionParameters _parameters;

        public int Latency { get; private set; }

        public UnityBlendTreeBackend(ndmf.BuildContext buildContext, VirtualAnimatorController vac)
        {
            var asc = buildContext.Extension<AnimatorServicesContext>();
            AnimationIndex = asc.AnimationIndex;
            ObjectPathRemapper = asc.ObjectPathRemapper;
            
            EmptyMotion = VirtualClip.Create("Empty");
            _vac = vac;
            _parameters = _fallbackParameters;

            AlwaysOnClip = VirtualClip.Create("Base");

            RootTree = VirtualBlendTree.Create("Root");
            RootTree.BlendType = BlendTreeType.Direct;
            RootTree.NormalizedBlendValues = false;
            RootTree.UseAutomaticThresholds = false;

            RootTree.Children = RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = AlwaysOnClip,
                DirectBlendParameter = ALWAYS_ONE
            });

            // Base layer at lowest priority to hold initial active-state defaults
            var baseBlendTree = VirtualBlendTree.Create("BaseLayerTree");
            BaseLayerClip = VirtualClip.Create("BaseLayerClip");
            baseBlendTree.BlendType = BlendTreeType.Direct;
            baseBlendTree.NormalizedBlendValues = false;
            baseBlendTree.UseAutomaticThresholds = false;
            baseBlendTree.Children = baseBlendTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = BaseLayerClip,
                DirectBlendParameter = ALWAYS_ONE
            });
            BaseLayerTree = baseBlendTree;
        }

        private void Bake(IMotionNode root)
        {
            var rootMotion = root.Bake(this);

            RootTree.Children = RootTree.Children.Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = rootMotion,
                DirectBlendParameter = ALWAYS_ONE
            });
        }

        internal VirtualMotion BakeMotion(IMotionNode root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return root.Bake(this);
        }
        
        public string AddParameter(string prefix, float value)
        {
            return _parameters.AddParameter(prefix, value);
        }

        public void SetParameterInitialValue(string name, float value)
        {
            _parameters.SetParameterInitialValue(name, value);
        }

        public IDisposable LatencyScope(int frames)
        {
            var scope = new LatencyDisposable(this);
            Latency += frames;
            return scope;
        }

        private class LatencyDisposable : IDisposable
        {
            private readonly UnityBlendTreeBackend _context;
            private readonly int _originalLatency;

            public LatencyDisposable(UnityBlendTreeBackend context)
            {
                _context = context;
                _originalLatency = context.Latency;
            }

            public void Dispose()
            {
                _context.Latency = _originalLatency;
            }
        }

        public float GetParameterInitialValue(string parameterName)
        {
            return _parameters.GetParameterInitialValue(parameterName);
        }

        internal void EnsureParameterPresent(string parameterName, float defaultValue = 0)
        {
            if (_parameters.ParameterDefaults.ContainsKey(parameterName)) return;

            var value = _vac.Parameters.TryGetValue(parameterName, out var parameter)
                ? AnimatorParameterValue(parameter)
                : defaultValue;
            _parameters.EnsureParameter(parameterName, value);
        }

        private static float AnimatorParameterValue(AnimatorControllerParameter parameter)
        {
            return parameter.type switch
            {
                AnimatorControllerParameterType.Bool => parameter.defaultBool ? 1 : 0,
                AnimatorControllerParameterType.Int => parameter.defaultInt,
                _ => parameter.defaultFloat
            };
        }

        public void PreprocessGraph(ReactionGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            _parameters = graph.Parameters;

            foreach (var node in graph.Nodes)
            {
                var expression = node.Expression;
                RegisterParameterDefaults(ref expression);
                node.Expression = expression;
            }

            ProcessExternalObjectStateInputsTransform.Apply(graph, this);
        }

        private void RegisterParameterDefaults(ref IExpression expression)
        {
            if (expression is ParameterExpression parameter)
            {
                _parameters.EnsureParameter(parameter.ParameterName, 0);
                if (_vac.Parameters.TryGetValue(parameter.ParameterName, out var existing))
                {
                    var existingValue = AnimatorParameterValue(existing);
                    _parameters.SetParameterInitialValue(parameter.ParameterName, existingValue);
                }

                return;
            }

            expression.Walk(RegisterParameterDefaults);
        }

        public void Build(IEnumerable<ReactionGraph> graphs)
        {
            if (graphs == null) throw new ArgumentNullException(nameof(graphs));
            var materialized = graphs.ToList();
            var firstNonEmptyGraph = materialized.FirstOrDefault(graph => graph.Nodes.Count > 0);
            if (firstNonEmptyGraph != null)
            {
                var parameters = firstNonEmptyGraph.Parameters;
                if (materialized.Any(graph => !ReferenceEquals(graph.Parameters, parameters)))
                    throw new InvalidOperationException("Reaction graphs do not share ReactionParameters");
                _parameters = parameters;
            }

            foreach (var graph in materialized)
            {
                var groups = AlignNodesTransform.CreateEffectGroups(this, graph);
                var aligned = AlignNodesTransform.Apply(this, groups);
                AssignInitialGroupStatesTransform.Apply(this, aligned);
                foreach (var group in aligned) Bake(group.Emit());
            }

            CommitParameters();
        }

        /// <summary>
        ///     Removes internal RC parameters from the VAC that are no longer referenced by any
        ///     node in the graph after pruning. This prevents orphaned parameters (e.g. ObjActive/X
        ///     parameters whose EffectGroups were removed by PruneUnusedInternalParametersTransform)
        ///     from remaining in the animator with stale or incorrect default values.
        /// </summary>
        private void CommitParameters()
        {
            const string rcPrefix = "$$MA/RC/";
            const string delayPrefix = "$$MA/RC/DELAY/";
            var orphans = _vac.Parameters.Keys
                .Where(k => k.StartsWith(rcPrefix)
                            && !k.StartsWith(delayPrefix)
                            && k != ALWAYS_ONE
                            && !_parameters.ParameterDefaults.ContainsKey(k))
                .ToList();

            // Build the pruned dictionary first, then assign once.
            // Assigning to _vac.Parameters is O(n) (it triggers a parameter-change callback),
            // so we batch all removals through Aggregate before the single assignment.
            var parameters = orphans.Aggregate(_vac.Parameters, (dict, name) => dict.Remove(name));
            foreach (var (name, value) in _parameters.ParameterDefaults)
                parameters = parameters.SetItem(name, new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = value });
            parameters = parameters.SetItem(ALWAYS_ONE, new AnimatorControllerParameter { name = ALWAYS_ONE, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            _vac.Parameters = parameters;
        }
    }
}
