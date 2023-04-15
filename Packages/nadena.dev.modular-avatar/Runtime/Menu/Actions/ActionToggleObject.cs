using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using Object = System.Object;

namespace nadena.dev.modular_avatar.core
{
    [AddComponentMenu("Modular Avatar/MA Action Toggle Object")]
    [RequireComponent(typeof(ActionController))]
    public class ActionToggleObject : AvatarTagComponent, SwitchedMenuAction
    {
        [Serializable]
        public class ObjectEntry
        {
            public GameObject target;
            public bool Active;
        }

        public List<ObjectEntry> Objects;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (Objects == null)
            {
                Objects = new List<ObjectEntry>();
            }
        }

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

        public bool BindsParameter(TargetParameter parameter)
        {
            return parameter == TargetParameter.BaseParameter;
        }
    }
}