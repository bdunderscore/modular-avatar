#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.ndmf.animator;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal static class EffectGroupDumper
    {
        internal static void DumpEffectGroups(IEnumerable<EffectGroup> groups, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ILBuild] EffectGroups {label}:");
            foreach (var group in groups)
            {
                var depthStr = group.Depth.HasValue ? $", depth={group.Depth}" : "";
                sb.AppendLine($"  target={group.TargetKey} [latency={group.Latency}{depthStr}]");
                for (var i = 0; i < group.Nodes.Count; i++)
                {
                    var node = group.Nodes[i];
                    var effect = node.Effects.FirstOrDefault(e => e.TargetKey.Equals(group.TargetKey));
                    sb.AppendLine(
                        $"    [{i}] priority={node.Priority}: {FormatExpression(node.Expression)} -> {effect}");
                }
            }

            Debug.Log(sb.ToString());
        }

        internal static void DumpNodeGraph(List<IMotionNode> nodes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ILBuild] Node graph before motion tree generation:");
            for (var i = 0; i < nodes.Count; i++)
            {
                sb.Append($"  [{i}] ");
                DumpInline(nodes[i], "  ", sb, 0);
            }

            Debug.Log(sb.ToString());
        }

        // Formats an IExpression, compacting AND(param > lo, param < hi) into "(lo < param < hi)".
        private static string FormatExpression(IExpression? expr)
        {
            if (expr is AndNode and)
            {
                ParameterExpression? lo = null, hi = null;
                List<IExpression>? rest = null;
                foreach (var child in and.Children)
                {
                    if (child is ParameterExpression pe)
                    {
                        if (pe.Mode == ParameterExpression.ConditionMode.GreaterThan && lo == null)
                        {
                            lo = pe;
                            continue;
                        }

                        if (pe.Mode == ParameterExpression.ConditionMode.LessThan && hi == null)
                        {
                            hi = pe;
                            continue;
                        }
                    }

                    (rest ??= new List<IExpression>()).Add(child);
                }

                if (lo != null && hi != null && lo.ParameterName == hi.ParameterName)
                {
                    var range = $"({lo.Threshold:G6} < {lo.ParameterName} < {hi.Threshold:G6})";
                    if (rest == null) return range;
                    return $"AND({range}, {string.Join(", ", rest.Select(FormatExpression))})";
                }
            }

            return expr?.ToString() ?? "(null)";
        }

        private static IMotionNode? Unwrap(IMotionNode? node)
        {
            while (node is ProxyNode pn && pn.Target != null)
                node = pn.Target;
            return node;
        }

        // Returns a one-line string for leaf nodes, or null if the node needs a block.
        private static string? DescribeLeaf(IMotionNode? node)
        {
            if (node == null) return "(null)";
            if (node is EmptyNode) return "(empty)";
            if (node is ProxyNode) return "(null proxy)";
            if (node is MotionNode mn)
            {
                if (mn.Motion is VirtualClip vc)
                {
                    var bindings = vc.GetFloatCurveBindings().ToList();
                    if (bindings.Count == 1)
                    {
                        var b = bindings[0];
                        var obj = string.IsNullOrEmpty(b.path)
                            ? ""
                            : b.path.Split('/').Last() + ".";
                        return $"clip({obj}{b.propertyName})";
                    }

                    return $"clip(\"{vc.Name}\", {bindings.Count} curves)";
                }

                return "motion(leaf)";
            }

            return null;
        }

        // Collects non-empty leaf segments from a single-param branch subtree.
        // Returns null if the tree mixes parameters (caller should fall back to raw dump).
        private static List<(float? lo, float? hi, IMotionNode motion, string param)>? TryCollectSegments(
            IMotionNode? node, string? param, float? lo, float? hi)
        {
            node = Unwrap(node);
            if (node == null || node is EmptyNode)
                return new List<(float?, float?, IMotionNode, string)>();

            if (node is BranchNode bn)
            {
                if (param == null) param = bn.Parameter;
                else if (bn.Parameter != param) return null;

                var left = TryCollectSegments(bn.OnLessThan, param, lo, bn.Threshold);
                if (left == null) return null;
                var right = TryCollectSegments(bn.OnGreaterEquals, param, bn.Threshold, hi);
                if (right == null) return null;

                left.AddRange(right);
                return left;
            }

            return new List<(float?, float?, IMotionNode, string)> { (lo, hi, node, param ?? "?") };
        }

        private static string FormatRange(string param, float? lo, float? hi)
        {
            if (lo == null && hi == null) return param;
            if (lo == null) return $"{param} < {hi:G6}";
            if (hi == null) return $"{param} >= {lo:G6}";
            return $"{param} in [{lo:G6}, {hi:G6})";
        }

        // Like FormatRange but omits the parameter name (for use when the name is already shown as a header).
        private static string FormatRangeBounds(float? lo, float? hi)
        {
            if (lo == null && hi == null) return "*";
            if (lo == null) return $"< {hi:G6}";
            if (hi == null) return $">= {lo:G6}";
            return $"[{lo:G6}, {hi:G6})";
        }

        // Collects the ranges in a ProxyCondition's condition tree that route to onTrueProxy.
        // Starting node should be cond.ProxyNode so the Always() case (where _node == onTrueProxy) works.
        // Returns null when the tree structure is not a pure single-param branch tree.
        private static List<(float? lo, float? hi, string param)>? TryCollectTrueSegments(
            IMotionNode? node, string? param, float? lo, float? hi,
            ProxyNode onFalseProxy, ProxyNode onTrueProxy)
        {
            // Check proxy identity before unwrapping.
            if (ReferenceEquals(node, onTrueProxy))
                return new List<(float?, float?, string)> { (lo, hi, param ?? "?") };
            if (ReferenceEquals(node, onFalseProxy))
                return new List<(float?, float?, string)>();

            var unwrapped = Unwrap(node);
            if (unwrapped == null || unwrapped is EmptyNode)
                return new List<(float?, float?, string)>();

            if (unwrapped is BranchNode bn)
            {
                if (param == null) param = bn.Parameter;
                else if (bn.Parameter != param) return null;

                var left = TryCollectTrueSegments(bn.OnLessThan, param, lo, bn.Threshold, onFalseProxy, onTrueProxy);
                if (left == null) return null;
                var right = TryCollectTrueSegments(bn.OnGreaterEquals, param, bn.Threshold, hi, onFalseProxy,
                    onTrueProxy);
                if (right == null) return null;

                left.AddRange(right);
                return left;
            }

            return null;
        }

        private static string FormatCondition(ProxyCondition cond)
        {
            // cond.ProxyNode is the outer ProxyNode (_node); using it as the start lets the
            // Always() case (where _node == onTrueProxy) be detected by reference equality.
            var segs = TryCollectTrueSegments(
                cond.ProxyNode, null, null, null, cond.OnFalseProxy, cond.OnTrueProxy);

            if (segs == null) return cond.Node?.GetType().Name ?? "(complex)";
            if (segs.Count == 0) return "never";
            if (segs.Count == 1) return FormatRange(segs[0].param, segs[0].lo, segs[0].hi);
            return segs[0].param + ": " + string.Join(" | ", segs.Select(s => FormatRangeBounds(s.lo, s.hi)));
        }

        // Writes the node inline if it's a leaf, otherwise starts a new line and indents a block.
        private static void DumpInline(IMotionNode node, string indent, StringBuilder sb, int depth)
        {
            var leaf = DescribeLeaf(Unwrap(node));
            if (leaf != null)
            {
                sb.AppendLine(leaf);
            }
            else
            {
                sb.AppendLine();
                DumpMotionNode(node, indent + "  ", sb, depth);
            }
        }

        private static void DumpMotionNode(IMotionNode node, string indent, StringBuilder sb, int depth)
        {
            if (depth > 32)
            {
                sb.AppendLine(indent + "...");
                return;
            }

            var unwrapped = Unwrap(node);
            var leaf = DescribeLeaf(unwrapped);
            if (leaf != null)
            {
                sb.AppendLine(indent + leaf);
                return;
            }

            if (unwrapped == null)
            {
                sb.AppendLine(indent + "(null)");
                return;
            }

            switch (unwrapped)
            {
                case PriorityNode prio:
                    sb.AppendLine(indent + $"Priority({prio.Conditions.Count} conds, latency={prio.Latency})");
                    sb.Append(indent + "  default: ");
                    DumpInline(prio.DefaultMotion, indent + "  ", sb, depth + 1);
                    for (var i = 0; i < prio.Conditions.Count; i++)
                    {
                        var (cond, motion) = prio.Conditions[i];
                        sb.Append(indent + $"  [{i}] (init={cond.InitialState}) {FormatCondition(cond)}: ");
                        DumpInline(motion, indent + "  ", sb, depth + 1);
                    }

                    break;

                case BranchNode bn:
                    var segs = TryCollectSegments(unwrapped, null, null, null);
                    if (segs == null)
                    {
                        // Mixed parameters — fall back to raw two-branch display.
                        sb.AppendLine(indent + $"Branch({bn.Parameter} >= {bn.Threshold:G6})");
                        sb.Append(indent + "  true:  ");
                        DumpInline(bn.OnGreaterEquals, indent + "  ", sb, depth + 1);
                        sb.Append(indent + "  false: ");
                        DumpInline(bn.OnLessThan, indent + "  ", sb, depth + 1);
                    }
                    else if (segs.Count == 0)
                    {
                        sb.AppendLine(indent + "(empty)");
                    }
                    else if (segs.Count == 1)
                    {
                        var (lo, hi, motion, param) = segs[0];
                        sb.Append(indent + FormatRange(param, lo, hi) + ": ");
                        DumpInline(motion, indent + "  ", sb, depth + 1);
                    }
                    else
                    {
                        sb.AppendLine(indent + segs[0].param + ":");
                        foreach (var (lo, hi, motion, _) in segs)
                        {
                            sb.Append(indent + "  " + FormatRangeBounds(lo, hi) + ": ");
                            DumpInline(motion, indent + "    ", sb, depth + 1);
                        }
                    }

                    break;

                default:
                    sb.AppendLine(indent + unwrapped.GetType().Name + $" (latency={unwrapped.Latency})");
                    break;
            }
        }
    }
}
