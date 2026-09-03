#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.Transformations
{
    internal static class AssignInitialGroupStatesTransform
    {
        internal static void Apply(UnityBlendTreeBackend backend, List<EffectGroup> groups)
        {
            foreach (var group in groups)
            {
                foreach (var (node, index) in group.Nodes.Select((n, i) => (n, i)))
                {
                    if (node.Expression.Evaluate(backend.GetParameterInitialValue))
                    {
                        group.DefaultNode = index;
                    }
                }

                if (group.DefaultNode.HasValue)
                {
                    var defaultNode = group.DefaultNode.Value;
                    backend.ApplyBaseState(
                        group.Nodes[defaultNode].Effects.First(e => e.TargetKey.Equals(group.TargetKey)),
                        true);
                }
                else
                {
                    backend.ApplyBaseState(
                        group.Nodes.SelectMany(n => n.Effects)
                            .First(e => e.TargetKey.Equals(group.TargetKey)),
                        false);
                }
            }
        }
    }
}
