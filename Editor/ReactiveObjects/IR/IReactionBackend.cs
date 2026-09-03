#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal interface IReactionBackend
    {
        /// <summary>
       ///     Enriches a complete reaction graph with backend-specific data before optimization.
       /// </summary>
       /// <param name="graph">The graph to preprocess.</param>
        void PreprocessGraph(ReactionGraph graph);

        /// <summary>
       ///     Allocates a uniquely named parameter with the specified initial value.
       /// </summary>
       /// <param name="prefix">The prefix used to generate the parameter name.</param>
       /// <param name="initialValue">The parameter's initial value.</param>
       /// <returns>The generated parameter name.</returns>
        string AddParameter(string prefix, float initialValue);

        /// <summary>
       ///     Gets the initial value of a parameter, or zero if the parameter is unknown.
       /// </summary>
       /// <param name="name">The parameter name.</param>
       /// <returns>The parameter's initial value.</returns>
        float GetParameterInitialValue(string name);

        /// <summary>
        ///     Sets the initial value of a parameter, adding the parameter if necessary.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The initial value.</param>
        void SetParameterInitialValue(string name, float value);

        /// <summary>
        ///     Lowers optimized reaction graphs into the backend's output representation.
        /// </summary>
        /// <param name="graphs">The optimized reaction subgraphs to build.</param>
        void Build(IEnumerable<ReactionGraph> graphs);
    }
}
