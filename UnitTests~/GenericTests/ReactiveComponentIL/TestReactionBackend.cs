using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace UnitTestsReactiveComponentIL
{
    internal sealed class TestReactionBackend : IReactionBackend
    {
        private ReactionParameters _parameters;

        internal TestReactionBackend(ReactionParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        public void PreprocessGraph(ReactionGraph graph)
        {
            _parameters = graph?.Parameters ?? throw new ArgumentNullException(nameof(graph));
        }

        public string AddParameter(string prefix, float initialValue)
        {
            return _parameters.AddParameter(prefix, initialValue);
        }

        public float GetParameterInitialValue(string name)
        {
            return _parameters.GetParameterInitialValue(name);
        }

        public void SetParameterInitialValue(string name, float value)
        {
            _parameters.SetParameterInitialValue(name, value);
        }

        public void Build(IEnumerable<ReactionGraph> graphs)
        {
            throw new NotSupportedException("The parameter-only test backend cannot build reaction graphs");
        }
    }
}
