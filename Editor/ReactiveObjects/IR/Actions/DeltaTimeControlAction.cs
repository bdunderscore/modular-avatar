#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Object = UnityEngine.Object;

namespace nadena.dev.modular_avatar.core.editor.rc.Actions
{
    internal class DeltaTimeControlAction : IAction
    {
        public sealed class Target
        {
            public Object TargetObject { get; }
            public string AntiSelectedProp { get; }

            public Target(Object targetObject, string antiSelectedProp)
            {
                TargetObject = targetObject;
                AntiSelectedProp = antiSelectedProp;
            }

            private bool Equals(Target other)
            {
                return TargetObject.Equals(other.TargetObject) && AntiSelectedProp == other.AntiSelectedProp;
            }

            public override bool Equals(object obj)
            {
                return ReferenceEquals(this, obj) || (obj is Target other && Equals(other));
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TargetObject, AntiSelectedProp);
            }
        }

        public Object TargetObject { get; set; }

        public float TransitionDuration { get; set; }
        public string SelectedProp { get; set; }
        public string AntiSelectedProp { get; set; }
        public ImmutableDictionary<string, float> ConstProps { get; set; } = ImmutableDictionary<string, float>.Empty;

        public object TargetKey => new Target(TargetObject, AntiSelectedProp);

        public DeltaTimeControlAction(Object targetObject, float transitionDuration, string selectedProp,
            string antiSelectedProp)
        {
            TargetObject = targetObject;
            TransitionDuration = transitionDuration;
            SelectedProp = selectedProp;
            AntiSelectedProp = antiSelectedProp;
        }

        public bool ApproximatelyEqual(IAction other)
        {
            throw new NotImplementedException();
        }

        public StaticApplyResult ApplyStatic(StaticApplyContext context)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IAction> Simplify()
        {
            if (TransitionDuration <= 0)
            {
                yield return new FloatPropAction(new PropertyTarget(TargetObject, SelectedProp), 1.0f);
                yield return new FloatPropAction(new PropertyTarget(TargetObject, AntiSelectedProp), 0.0f);
                foreach (var (k, v) in ConstProps)
                {
                    yield return new FloatPropAction(new PropertyTarget(TargetObject, k), v);
                }
            }
            else
            {
                yield return this;
            }
        }
    }
}