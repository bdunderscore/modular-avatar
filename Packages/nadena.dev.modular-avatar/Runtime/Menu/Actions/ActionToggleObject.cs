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
            public AvatarObjectReference target;
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
                    new MenuCurveBinding(obj.target.Get(this), typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1, obj.Active ? 1 : 0))
            ).ToImmutableDictionary();
        }

        public ImmutableDictionary<MenuCurveBinding, AnimationCurve> GetInactiveCurves()
        {
            var builder = ImmutableDictionary<MenuCurveBinding, AnimationCurve>.Empty.ToBuilder();

            foreach (var obj in Objects)
            {
                var target = obj.target?.Get(this);

                if (target == null) continue;

                builder.Add(
                    new MenuCurveBinding(target, typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1, 0)
                );
            }

            return builder.ToImmutable();
        }

        public bool BindsParameter(TargetParameter parameter)
        {
            return parameter == TargetParameter.BaseParameter;
        }
    }
}