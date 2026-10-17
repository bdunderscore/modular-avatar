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
using UnityEngine;
using Object = UnityEngine.Object;
#if MA_VRCSDK3_AVATARS
using nadena.dev.ndmf.vrchat;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     The Merge Motion pass injects a layer at int.MinValue; we need to come in before that layer,
    ///     but also process the FX layer after that layer is added. As such, we inject a placeholder before
    ///     Merge Motion runs in this pass.
    /// </summary>
    [DependsOnContext(typeof(AnimatorServicesContext))]
    internal class InjectROBaseLayer : IExtensionContext
    {
        public VirtualLayer? Layer;

        public void OnActivate(ndmf.BuildContext context)
        {
#if MA_VRCSDK3_AVATARS
            if (context.PlatformProvider.QualifiedName != WellKnownPlatforms.VRChatAvatar30) return;

            var asc = context.Extension<AnimatorServicesContext>();
            if (asc.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fxLayer))
            {
                Layer = fxLayer.AddLayer(new LayerPriority(int.MinValue), VRChatBlendTreeBackend.BaseLayerName);
            }
#endif
        }

        public void OnDeactivate(ndmf.BuildContext context)
        {
#if MA_VRCSDK3_AVATARS
            if (Layer != null && Layer.StateMachine?.DefaultState?.Motion == null)
            {
                var asc = context.Extension<AnimatorServicesContext>();
                asc.ControllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.FX].RemoveLayer(Layer);
            }
#endif
        }
    }

    [DependsOnContext(typeof(InjectROBaseLayer))]
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

            if (analysis.Shapes.Count == 0) return;
            
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

#if MA_VRCSDK3_AVATARS
            if (backend is VRChatBlendTreeBackend vrcBackend)
            {
                var baseLayer = context.Extension<InjectROBaseLayer>().Layer;
                if (baseLayer != null)
                {
                    if (!vrcBackend.BaseBlendTree.Children.IsEmpty)
                    {
                        var state = baseLayer.StateMachine!.AddState("Base");
                        baseLayer.StateMachine.DefaultState = state;
                        state.Motion = vrcBackend.BaseBlendTree;
                    }
                }

                var avDesc = context.VRChatAvatarDescriptor()!;
                var expParams = avDesc.expressionParameters;
                if (expParams == null)
                {
                    expParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    expParams.parameters = null;
                    expParams.name = "Expression Parameters";
                    context.AssetSaver.SaveAsset(expParams);
                } else if (!context.IsTemporaryAsset(expParams))
                {
                    expParams = Object.Instantiate(expParams);
                    context.AssetSaver.SaveAsset(expParams);
                    avDesc.expressionParameters = expParams;
                }

                if (expParams.parameters == null)
                {
                    expParams.parameters = vrcBackend.ExtraSyncParams.ToArray();
                }
                else
                {
                    expParams.parameters = expParams.parameters.Concat(vrcBackend.ExtraSyncParams).ToArray();
                }
            }
#endif
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
