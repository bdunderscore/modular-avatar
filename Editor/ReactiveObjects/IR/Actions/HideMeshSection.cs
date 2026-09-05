#nullable enable

using System;
using nadena.dev.modular_avatar.core.editor.rc.Graph;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal sealed class HideMeshSection : IAction
    {
        public MeshSectionTarget Target { get; }
        public IMeshSelector? Selector { get; }
        public bool ShouldHide => Selector != null;

        public object TargetKey => Target;

        public HideMeshSection(MeshSectionTarget target, IMeshSelector? selector)
        {
            Target = target;
            Selector = selector;
        }

        public static HideMeshSection Retain(MeshSectionTarget target)
        {
            return new HideMeshSection(target, null);
        }


        public bool ApproximatelyEqual(IAction other)
        {
            return Equals(other);
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            if (Selector == null) return StaticApplyResult.Applied;
            if (Target.Renderer == null || Target.Renderer.sharedMesh == null)
                return StaticApplyResult.Retain;

            context.Get<SkinnedMeshRenderer, StaticMeshRemoval>(
                Target.Renderer,
                renderer => new StaticMeshRemoval(renderer)
            ).Add(Selector);
            return StaticApplyResult.Applied;
        }

        private bool Equals(HideMeshSection other)
        {
            return Target.Equals(other.Target) && Equals(Selector, other.Selector);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is HideMeshSection other && Equals(other));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Target, Selector);
        }

        public override string ToString()
        {
            return $"HideMeshSection({Target}, {Selector})";
        }
    }
}