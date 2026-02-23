#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactiveObjectPassV2
    {
        private readonly ndmf.BuildContext context;
        private readonly AnimatorServicesContext asc;
        private readonly BakeContext _bakeContext;

        public ReactiveObjectPassV2(ndmf.BuildContext context)
        {
            this.context = context;
            asc = context.Extension<AnimatorServicesContext>();

#if MA_VRCSDK3_AVATARS
            var controller = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
#else
            // Disable animation generation, but still evaluate initial states
            VirtualAnimatorController? controller = null;
#endif

            _bakeContext = new BakeContext(context, controller);
        }

        internal void Execute()
        {
            var analysis = new ReactiveObjectAnalyzer(context).Analyze(context.AvatarRootObject);

            var shapes = analysis.Shapes;

            ILBuild.Build(_bakeContext, ShapeToGraph(shapes));
        }

        private ReactionGraph ShapeToGraph(Dictionary<TargetProp, AnimatedProperty> shapes)
        {
            var graph = new ReactionGraph();

            // TODO - handle overrideStaticState
            foreach (var prop in shapes.Values)
            {
                foreach (var rule in prop.actionGroups)
                {
                    IAction action;
                    if (rule.TargetProp.TargetObject is GameObject go && rule.TargetProp.PropertyName == "m_IsActive")
                    {
                        action = new DriveActiveState(go, (float)rule.Value! > 0.5f);
                    }
                    else
                    {
                        action = new PropAction(rule.TargetProp, rule.Value);
                    }

                    var conditions = rule.ControllingConditions.Select(ConvertCondition).ToArray();
                    IExpression expr = new AndNode(conditions);
                    if (rule.Inverted)
                    {
                        expr = new NotNode(expr);
                    }

                    graph.AddNode(new ReactionNode(expr, action));
                }
            }

            return graph;
        }

        private IExpression ConvertCondition(ControlCondition arg)
        {
            if (arg.ReferenceObject != null)
            {
                return new ObjectActiveState(arg.ReferenceObject, ObjectActiveState.State.Active);
            }

            // TODO - find correct initial value here
            _bakeContext.EnsureParameterPresent(arg.Parameter);

            if (!float.IsFinite(arg.ParameterValueHi))
            {
                return new ParameterExpression(arg.Parameter, arg.ParameterValueLo);
            }

            if (!float.IsFinite(arg.ParameterValueLo))
            {
                return new ParameterExpression(arg.Parameter, arg.ParameterValueHi,
                    ParameterExpression.ConditionMode.LessThan);
            }

            var c1 = new ParameterExpression(arg.Parameter, arg.ParameterValueLo);
            var c2 = new ParameterExpression(arg.Parameter, arg.ParameterValueHi,
                ParameterExpression.ConditionMode.LessThan);
            return new AndNode(c1, c2);
        }
    }
}