#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    internal sealed class ObjectActiveState : IExpression
    {
        public enum State
        {
            NotDriven,
            Active,
            Inactive
        }

        public GameObject TargetObject { get; set; }
        public State StateMode { get; set; }

        public ObjectActiveState(GameObject? targetObject, State? mode = null)
        {
            TargetObject = targetObject != null ? targetObject : throw new ArgumentNullException(nameof(targetObject));
            StateMode = mode ?? State.Active;
        }

        public IExpression DeepClone()
        {
            return new ObjectActiveState(TargetObject, StateMode);
        }

        public bool Evaluate(Func<string, float> getParameter)
        {
            return StateMode switch
            {
                State.Active => TargetObject.activeSelf,
                State.Inactive => !TargetObject.activeSelf,
                // NotDriven should be rewritten to parameter-based conditions before evaluation.
                State.NotDriven => false,
                _ => TargetObject.activeSelf
            };
        }

        public void Walk(ExpressionVisitor visitor)
        {
            // leaf node
        }

        public override string ToString()
        {
            return TargetObject != null ? $"ObjectActive({TargetObject.name})" : "ObjectActive(null)";
        }

        public override bool Equals(object? obj)
        {
            return obj is ObjectActiveState other &&
                   TargetObject == other.TargetObject &&
                   StateMode == other.StateMode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TargetObject, StateMode);
        }
    }
}
