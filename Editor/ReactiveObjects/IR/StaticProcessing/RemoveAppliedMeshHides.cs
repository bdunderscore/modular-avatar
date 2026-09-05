#nullable enable

using System.Collections.Generic;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;

namespace nadena.dev.modular_avatar.core.editor.rc.StaticProcessing
{
    internal static class RemoveAppliedMeshHides
    {
        internal static void Apply(ReactionGraph graph)
        {
            var removedShapes = new HashSet<ShapeKeyTarget>();
            foreach (var node in graph.Nodes)
            {
                foreach (var effect in node.Effects)
                {
                    if (effect is AlreadyApplied
                        {
                            Inner: HideMeshSection
                            {
                                ShouldHide: true
                            } hide
                        } &&
                        hide.Target.IsShape &&
                        hide.Target.ShapeName is { } shapeName)
                    {
                        removedShapes.Add(new ShapeKeyTarget(hide.Target.Renderer, shapeName));
                    }
                }
            }

            foreach (var node in graph.Nodes)
            {
                node.Effects.RemoveAll(effect =>
                    effect is AlreadyApplied { Inner: HideMeshSection } ||
                    effect.TargetKey is ShapeKeyTarget shape && removedShapes.Contains(shape));
            }

            graph.Nodes.RemoveAll(node => node.Effects.Count == 0);
        }
    }
}
