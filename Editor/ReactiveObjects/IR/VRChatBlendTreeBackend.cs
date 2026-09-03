#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;

namespace nadena.dev.modular_avatar.core.editor.rc
{
    internal sealed class VRChatBlendTreeBackend : IReactionBackend
    {
        internal const string BaseLayerName = "MA/RC Base";
        internal const string ApplyLayerName = "MA/RC Apply";

        private readonly VirtualAnimatorController _fxController;
        private readonly UnityBlendTreeBackend _inner;

        public VRChatBlendTreeBackend(VirtualAnimatorController fxController, UnityBlendTreeBackend inner)
        {
            _fxController = fxController ?? throw new ArgumentNullException(nameof(fxController));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void PreprocessGraph(ReactionGraph graph)
        {
            _inner.PreprocessGraph(graph);
        }

        public string AddParameter(string prefix, float initialValue)
        {
            return _inner.AddParameter(prefix, initialValue);
        }

        public float GetParameterInitialValue(string name)
        {
            return _inner.GetParameterInitialValue(name);
        }

        public void SetParameterInitialValue(string name, float value)
        {
            _inner.SetParameterInitialValue(name, value);
        }

        public void Build(IEnumerable<ReactionGraph> graphs)
        {
            var graphList = graphs.ToList();
            _inner.Build(graphList);

            InstallLayer(BaseLayerName, int.MinValue, "Base", _inner.BaseLayerTree);
            InstallLayer(ApplyLayerName, 1, "Apply", _inner.RootTree);
        }

        private void InstallLayer(string layerName, int priority, string stateName, VirtualMotion motion)
        {
            var layer = _fxController.AddLayer(new LayerPriority(priority), layerName);
            layer.BlendingMode = AnimatorLayerBlendingMode.Override;
            layer.DefaultWeight = 1;

            var stateMachine = layer.StateMachine ??
                               throw new InvalidOperationException("Animator layer was created without a state machine");
            var state = stateMachine.AddState(stateName);
            stateMachine.DefaultState = state;
            state.Motion = motion;
        }
    }
}
