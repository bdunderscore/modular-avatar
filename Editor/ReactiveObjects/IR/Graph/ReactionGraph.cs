#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Transformations;

namespace nadena.dev.modular_avatar.core.editor.rc.Graph
{
    public class ReactionGraph
    {
        public List<ReactionNode> Nodes = new();

        internal void AddNode(ReactionNode node)
        {
            Nodes.Add(node);
        }

        /// <summary>
        ///     Breaks down all multi-effect nodes into single-effect nodes
        /// </summary>
        public void Decompose()
        {
            DecomposeTransform.Apply(this);
        }

        public Dictionary<object, List<ReactionNode>> NodesByTarget()
        {
            return Nodes.SelectMany(n => n.Effects.Select(e => (e.TargetKey, n)))
                .GroupBy(p => p.TargetKey)
                .Select(g => (g.Key, g.Select(p => p.n).ToList()))
                .ToDictionary(kv => kv.Key, kv => kv.Item2);
        }

        /// <summary>
        ///     Performs a prepass to identify and import external animations which influence game object active states.
        ///     This pass will add parameter curves to preexisting animations which manipulate game object active states,
        ///     and rewrite expressions to use these parameters. After completing this pass, all ObjectActiveState expressions
        ///     only need to consider effects from within the RC framework.
        ///
        ///     This pass also replaces undriven object state nodes with constant nodes.
        /// </summary>
        /// <param name="context"></param>
        public void ProcessExternalObjectStateInputs(BakeContext context)
        {
            ProcessExternalObjectStateInputsTransform.Apply(this, context);
        }

        /// <summary>
        ///     Simplifies boolean expressions by:
        ///     1. Removing constants inside AND and OR nodes
        ///     2. Collapsing AND-inside-AND and OR-inside-OR
        ///     3. Collapsing single-element AND and OR nodes
        ///     4. Collapsing nested NOT nodes, and constant-in-NOT nodes
        /// </summary>
        public void BooleanSimplify()
        {
            BooleanSimplifyTransform.Apply(this);
        }

        public List<ReactionGraph> SplitIntoSubgraphs()
        {
            return SplitIntoSubgraphsTransform.Apply(this);
        }

        public void ForwardObjectActiveDrivers()
        {
            ForwardObjectActiveDriversTransform.Apply(this);
        }

        /// <summary>
        ///     Converts all ObjectActiveState expressions into InternalParameterConditions, and all
        ///     ObjectActiveDriver nodes (DriveActiveState actions) into InternalParameterDrivers
        ///     (DriveInternalParameter actions). This is step 7 in the transformation pipeline.
        /// </summary>
        public void ConvertToInternalParameters(BakeContext context)
        {
            ConvertToInternalParametersTransform.Apply(this, context);
        }

        /// <summary>
        ///     Detects and breaks cycles in the reaction graph by replacing InternalParameterDrivers with
        ///     normal ParameterDrivers on nodes that are part of cycles. This is step 8 in the transformation pipeline.
        /// </summary>
        public void BreakLoops()
        {
            BreakLoopsTransform.Apply(this);
        }
    }
}
