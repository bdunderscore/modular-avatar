#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor.rc.Conditions
{
    public sealed class ObjectActiveState : IExpression
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
            TargetObject = targetObject ?? throw new ArgumentNullException(nameof(targetObject));
            StateMode = mode ?? State.Active;
        }

        public void Walk(ExpressionVisitor visitor)
        {
            // leaf node
        }

        public override string ToString()
        {
            return TargetObject != null ? $"ObjectActive({TargetObject.name})" : "ObjectActive(null)";
        }

        public override bool Equals(object obj)
        {
            return obj is ObjectActiveState other && TargetObject == other.TargetObject;
        }

        public override int GetHashCode()
        {
            return TargetObject?.GetHashCode() ?? 0;
        }
    }
}