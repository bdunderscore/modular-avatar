#if MA_VRCSDK3_AVATARS

using nadena.dev.modular_avatar.test.Tests;
using nadena.dev.modular_avatar.core.editor;
using nadena.dev.modular_avatar.core.editor.rc;
using nadena.dev.modular_avatar.core.editor.rc.Actions;
using nadena.dev.modular_avatar.core.editor.rc.Conditions;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType;

namespace nadena.dev.modular_avatar.test.ReactiveComponent
{
    internal class VRChatBlendTreeBackendTests : TestBase
    {
        [Test]
        public void SentinelDefault_UsesMatchingExpressionParameterDefault()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateExpressionParameters("menu", 1);
            var target = CreateChild(root, "Target");
            target.SetActive(false);

            var graph = CreateToggleGraph("menu", ControlCondition.UnselectedMenuItemInitialValue, target);
            var controller = Build(root, descriptor, graph);

            Assert.That(controller.Parameters["menu"].defaultFloat, Is.EqualTo(1));
            Assert.That(target.activeSelf, Is.True);
        }

        public enum ExpressionParameterAssetKind
        {
            NoAsset,
            UnrelatedAsset,
            NullParameterArray,
        }

        [TestCase(ExpressionParameterAssetKind.NoAsset)]
        [TestCase(ExpressionParameterAssetKind.UnrelatedAsset)]
        [TestCase(ExpressionParameterAssetKind.NullParameterArray)]
        public void SentinelDefault_WithoutExpressionParameter_FallsBackToZero(
            ExpressionParameterAssetKind expressionParameterAssetKind
        )
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = expressionParameterAssetKind switch
            {
                ExpressionParameterAssetKind.NoAsset => null,
                ExpressionParameterAssetKind.UnrelatedAsset => CreateExpressionParameters("other", 1),
                ExpressionParameterAssetKind.NullParameterArray => TrackObject(
                    ScriptableObject.CreateInstance<VRCExpressionParameters>()),
                _ => throw new System.ArgumentOutOfRangeException(nameof(expressionParameterAssetKind)),
            };
            if (descriptor.expressionParameters != null
                && expressionParameterAssetKind == ExpressionParameterAssetKind.NullParameterArray)
            {
                descriptor.expressionParameters.parameters = null;
            }
            var target = CreateChild(root, "Target");
            target.SetActive(false);

            var graph = CreateToggleGraph("menu", ControlCondition.UnselectedMenuItemInitialValue, target);
            var controller = Build(root, descriptor, graph);

            Assert.That(controller.Parameters["menu"].defaultFloat, Is.Zero);
            Assert.That(target.activeSelf, Is.False);
        }

        [Test]
        public void GraphDefault_IsNotReplacedByExpressionParameterDefault()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateExpressionParameters("menu", 0);
            var target = CreateChild(root, "Target");
            target.SetActive(false);

            var graph = CreateToggleGraph("menu", 1, target);
            var controller = Build(root, descriptor, graph);

            Assert.That(controller.Parameters["menu"].defaultFloat, Is.EqualTo(1));
            Assert.That(target.activeSelf, Is.True);
        }

        [Test]
        public void ExistingFxDefault_TakesPrecedenceOverExpressionParameterDefault()
        {
            var root = CreateRoot("root");
            AddMinimalAvatarComponents(root);
            var descriptor = root.GetComponent<VRCAvatarDescriptor>();
            descriptor.expressionParameters = CreateExpressionParameters("menu", 1);
            var target = CreateChild(root, "Target");
            target.SetActive(false);

            var graph = CreateToggleGraph("menu", ControlCondition.UnselectedMenuItemInitialValue, target);
            var controller = Build(root, descriptor, graph, controller =>
            {
                controller.Parameters = controller.Parameters.Add("menu", new AnimatorControllerParameter
                {
                    name = "menu",
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0
                });
            });

            Assert.That(controller.Parameters["menu"].defaultFloat, Is.Zero);
            Assert.That(target.activeSelf, Is.False);
        }

        private VRCExpressionParameters CreateExpressionParameters(string name, float defaultValue)
        {
            var parameters = TrackObject(ScriptableObject.CreateInstance<VRCExpressionParameters>());
            parameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = name,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = defaultValue
                }
            };
            return parameters;
        }

        private static ReactionGraph CreateToggleGraph(string parameter, float defaultValue, GameObject target)
        {
            var graph = new ReactionGraph();
            graph.Parameters.EnsureParameter(parameter, defaultValue);
            graph.AddNode(new ReactionNode(
                new ParameterExpression(parameter),
                new DriveActiveState(target, true)
            ));
            return graph;
        }

        private static VirtualAnimatorController Build(
            GameObject root,
            VRCAvatarDescriptor descriptor,
            ReactionGraph graph,
            System.Action<VirtualAnimatorController> setupController = null
        )
        {
            var animator = root.GetComponent<Animator>();
            var context = new nadena.dev.ndmf.BuildContext(root, null,
                PlatformRegistry.PlatformProviders[WellKnownPlatforms.VRChatAvatar30], true);
            var services = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controller = VirtualAnimatorController.Create(services.ControllerContext.CloneContext);
            services.ControllerContext.Controllers[FX] = controller;
            services.ControllerContext.Controllers[animator] = controller;
            var injectRO = context.ActivateExtensionContextRecursive<InjectROBaseLayer>();

            try
            {
                setupController?.Invoke(controller);

                var backend = new VRChatBlendTreeBackend(descriptor, controller, new UnityBlendTreeBackend(context, controller));
                backend.PreprocessGraph(graph);
                ILBuild.Simplify(graph);
                backend.Build(graph);

                var baseState = injectRO.Layer.StateMachine.AddState("Base");
                baseState.Motion = backend.BaseBlendTree;
                injectRO.Layer.StateMachine.DefaultState = baseState;
            }
            finally
            {
                context.DeactivateAllExtensionContexts();
            }

            return controller;
        }
    }
}

#endif
