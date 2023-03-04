using System;
using System.Collections.Immutable;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    public interface MenuAction
    {
        /// <summary>
        /// Returns whether this action binds to the given parameter.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        bool BindsParameter(TargetParameter parameter);
    }

    public interface SwitchedMenuAction : MenuAction
    {
        /// <summary>
        /// Returns the curves applied when this action is active
        /// </summary>
        /// <returns></returns>
        ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetCurves();

        /// <summary>
        /// Returns the curves applied when this action is inactive (and no other actions override).
        /// </summary>
        /// <param name="isDefault">True if this action is part of the default toggle option.</param>
        /// <returns></returns>
        ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetInactiveCurves(bool isDefault);
    }

    public enum TargetParameter
    {
        BaseParameter,
        RadialParam,
        Horizontal,
        Vertical,
        Up,
        Right,
        Down,
        Left
    }

    public static class TargetParameterExtension
    {
        public static int Index(this TargetParameter p)
        {
            switch (p)
            {
                case TargetParameter.BaseParameter:
                    return -1;
                case TargetParameter.RadialParam: return 0;
                case TargetParameter.Horizontal: return 0;
                case TargetParameter.Vertical: return 1;
                case TargetParameter.Up: return 0;
                case TargetParameter.Right: return 1;
                case TargetParameter.Down: return 2;
                case TargetParameter.Left: return 3;
                default: throw new NotImplementedException();
            }
        }
    }

    public sealed class MenuCurveBinding
    {
        public readonly GameObject target;
        public readonly Type type;
        public readonly string property;

        public MenuCurveBinding(GameObject target, Type type, string property)
        {
            this.target = target;
            this.type = type;
            this.property = property;
        }

        private bool Equals(MenuCurveBinding other)
        {
            return target == other.target && type == other.type && property == other.property;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MenuCurveBinding) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (target != null ? target.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (type != null ? type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (property != null ? property.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}