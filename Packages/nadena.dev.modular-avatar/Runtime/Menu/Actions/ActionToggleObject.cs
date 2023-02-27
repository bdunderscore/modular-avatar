using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
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

    public interface MenuAction
    {
        ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetCurves();
        ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetDefaultCurves();
    }

    [RequireComponent(typeof(ModularAvatarMenuItem))]
    public class ActionToggleObject : AvatarTagComponent, MenuAction
    {
        [Serializable]
        public class ObjectEntry
        {
            public GameObject target;
            public bool Active;
        }

        public List<ObjectEntry> Objects;

        public ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetCurves()
        {
            return Objects.Select(obj =>
                new KeyValuePair<MenuCurveBinding, AnimationCurve>(
                    new MenuCurveBinding(obj.target, typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1, obj.Active ? 1 : 0))
            ).ToImmutableDictionary();
        }

        public ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetDefaultCurves()
        {
            return Objects.Select(obj =>
                new KeyValuePair<MenuCurveBinding, AnimationCurve>(
                    new MenuCurveBinding(obj.target, typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1, obj.target.activeSelf ? 1 : 0))
            ).ToImmutableDictionary();
        }
    }
}