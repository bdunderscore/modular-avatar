#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    internal sealed class ReactionParameters
    {
        private const string RcPrefix = "$$MA/RC/";
        private const string DelayPrefix = "$$MA/RC/DELAY/";
        private const string AlwaysOne = "$$MA/RC/AlwaysOne";

        private readonly Dictionary<string, float> _parameterDefaults = new();
        private int _counter;

        public IReadOnlyDictionary<string, float> ParameterDefaults => _parameterDefaults;

        internal string AddParameter(string prefix, float initialValue)
        {
            var name = $"{RcPrefix}{prefix}${_counter++}";
            SetParameterInitialValue(name, initialValue);
            return name;
        }

        internal void EnsureParameter(string name, float initialValue)
        {
            if (!_parameterDefaults.ContainsKey(name))
            {
                _parameterDefaults.Add(name, initialValue);
            }
        }

        internal float GetParameterInitialValue(string name)
        {
            return _parameterDefaults.TryGetValue(name, out var value) ? value : 0;
        }

        internal void SetParameterInitialValue(string name, float value)
        {
            _parameterDefaults[name] = value;
        }

        internal void PruneOrphanedInternalParameters(ReactionGraph graph)
        {
            var survivingNames = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                foreach (var effect in node.Effects)
                {
                    switch (effect.TargetKey)
                    {
                        case InternalParameterTarget internalParameter:
                            survivingNames.Add(internalParameter.ParameterName);
                            break;
                        case ParameterTarget parameter:
                            survivingNames.Add(parameter.ParameterName);
                            break;
                    }
                }

                CollectRcParameterExpressions(node.Expression, survivingNames);
            }

            var orphanedNames = new List<string>();
            foreach (var name in _parameterDefaults.Keys)
            {
                if (name.StartsWith(RcPrefix)
                    && !name.StartsWith(DelayPrefix)
                    && name != AlwaysOne
                    && !survivingNames.Contains(name))
                {
                    orphanedNames.Add(name);
                }
            }

            foreach (var name in orphanedNames)
            {
                _parameterDefaults.Remove(name);
            }
        }

        private static void CollectRcParameterExpressions(IExpression expression, HashSet<string> names)
        {
            switch (expression)
            {
                case ParameterExpression parameter when parameter.ParameterName.StartsWith(RcPrefix):
                    names.Add(parameter.ParameterName);
                    break;
                default:
                    expression.Walk((ref IExpression child) => CollectRcParameterExpressions(child, names));
                    break;
            }
        }
    }
}
