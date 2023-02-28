using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using Object = System.Object;

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

        public ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetInactiveCurves(bool isDefault)
        {
            return Objects.Select(obj =>
                {
                    bool active;
                    if (isDefault)
                    {
                        active = !obj.Active; // inactive state is the opposite of the default state
                    }
                    else
                    {
                        active = obj.target.activeSelf; // inactive state is the current state
                    }

                    return new KeyValuePair<MenuCurveBinding, AnimationCurve>(
                        new MenuCurveBinding(obj.target, typeof(GameObject), "m_IsActive"),
                        AnimationCurve.Constant(0, 1, active ? 1 : 0));
                }
            ).ToImmutableDictionary();
        }
    }
}