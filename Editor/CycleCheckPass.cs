#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class CycleCheckPass : Pass<CycleCheckPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            // object -> becomes-child-of, reason
            Dictionary<Transform, (Transform, Component?)> edges = new();

            AddParentEdges(context, edges);
            AddBoneProxyEdges(context, edges);
            AddMergeArmatureEdges(context, edges);

            CheckForCycles(edges);
        }

        private void CheckForCycles(Dictionary<Transform, (Transform, Component?)> edges)
        {
            List<Transform> edgeTrace = new();
            Dictionary<Transform, int> visited = new();
            // Our cycle detection algorithm is quite simple - we select an arbitrary edge, then follow it to see if
            // we find a cycle. If not, we delete all edges traversed; then repeat until there are no edges.
            // If we do find a cycle, we report the components found in the cycle.

            while (edges.Count > 0)
            {
                edgeTrace.Clear();
                visited.Clear();

                var cursor = edges.First().Key;

                while (cursor != null)
                {
                    if (visited.TryGetValue(cursor, out var index))
                    {
                        // Cycle identified
                        ReportCycle(edgeTrace, edges, index);
                        break;
                    }

                    visited[cursor] = edgeTrace.Count;
                    edgeTrace.Add(cursor);

                    cursor = edges.GetValueOrDefault(cursor).Item1;
                }

                foreach (var t in edgeTrace)
                {
                    edges.Remove(t);
                }
            }
        }

        private void ReportCycle(List<Transform> edgeTrace, Dictionary<Transform, (Transform, Component?)> edges,
            int index)
        {
            List<object> causes = new();

            for (var i = index; i < edgeTrace.Count; i++)
            {
                var (_, cause) = edges[edgeTrace[i]];
                if (cause != null) causes.Add(cause);
            }

            BuildReport.LogFatal("error.object_cycle", causes.ToArray());
        }

        private void AddParentEdges(ndmf.BuildContext context, Dictionary<Transform, (Transform, Component?)> edges)
        {
            foreach (var transform in context.AvatarRootTransform.GetComponentsInChildren<Transform>(true))
            {
                if (transform == context.AvatarRootTransform) continue;

                edges[transform] = (transform.parent, null);
            }
        }

        private void AddBoneProxyEdges(ndmf.BuildContext context, Dictionary<Transform, (Transform, Component?)> edges)
        {
            foreach (var proxy in context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarBoneProxy>(true))
            {
                if (proxy.target != null)
                {
                    edges[proxy.transform] = (proxy.target, proxy);
                }
            }
        }

        private void AddMergeArmatureEdges(ndmf.BuildContext context,
            Dictionary<Transform, (Transform, Component?)> edges)
        {
            foreach (var mama in context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarMergeArmature>(true))
            {
                var target = mama.mergeTarget.Get(mama);
                if (target == null) continue;

                foreach (var (src, dst) in mama.GetBonesMapping())
                {
                    if (src != null && dst != null)
                    {
                        edges[src] = (dst, mama);
                    }
                }
            }
        }
    }
}