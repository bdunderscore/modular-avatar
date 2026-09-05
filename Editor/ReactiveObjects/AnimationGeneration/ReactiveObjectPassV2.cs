#nullable enable


using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.modular_avatar.core.editor.rc.StaticProcessing;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
#if MA_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactiveObjectPassV2
    {
        private readonly ndmf.BuildContext context;
        private readonly AnimatorServicesContext asc;

        public ReactiveObjectPassV2(ndmf.BuildContext context)
        {
            this.context = context;
            asc = context.Extension<AnimatorServicesContext>();
        }

        internal void Execute()
        {
            var analysis = new ReactiveObjectAnalyzer(context).Analyze(context.AvatarRootObject);
            var graph = ShapeToGraph(analysis.Shapes);

            IReactionBackend? backend = null;
#if MA_VRCSDK3_AVATARS
            if (context.PlatformProvider.QualifiedName == WellKnownPlatforms.VRChatAvatar30)
            {
                var controller = asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
                var innerBackend = new UnityBlendTreeBackend(context, controller);
                backend = new VRChatBlendTreeBackend(controller, innerBackend);
            }
#endif

            if (backend == null)
            {
                using var staticContext = new StaticApplyContext();
                foreach (var action in analysis.InitialActions.Values)
                {
                    action.ApplyStatic(staticContext);
                }

                return;
            }

            backend.PreprocessGraph(graph);
            ILBuild.Simplify(graph);
            StaticProcessing.Apply(graph);

            backend.Build(graph);
        }


        private ReactionGraph ShapeToGraph(IReadOnlyDictionary<object, AnimatedProperty> shapes)
        {
            var graph = new ReactionGraph();

            foreach (var property in shapes.Values)
            {
                foreach (var rule in property.actionGroups)
                {
                    var conditions = rule.ControllingConditions
                        .Select(condition => ConvertCondition(graph, condition))
                        .ToArray();
                    IExpression expression = new AndNode(conditions);
                    if (rule.Inverted)
                    {
                        expression = new NotNode(expression);
                    }

                    graph.AddNode(new ReactionNode(expression, rule.Action));
                }
            }

            return graph;
        }

        private IExpression ConvertCondition(ReactionGraph graph, ControlCondition arg)
        {
            if (arg.ReferenceObject != null)
            {
                return new ObjectActiveState(arg.ReferenceObject, ObjectActiveState.State.Active);
            }

            graph.Parameters.EnsureParameter(arg.Parameter, arg.InitialValue);

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
