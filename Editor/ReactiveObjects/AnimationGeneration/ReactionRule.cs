using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ReactionRule
    {
        public ReactionRule(ndmf.BuildContext context, TargetProp key, GameObject controllingObject, float value)
            : this(context, key, controllingObject, (object)value) { }
            
        public  ReactionRule(ndmf.BuildContext context, TargetProp key, GameObject controllingObject, UnityEngine.Object value)
            : this(context, key, controllingObject, (object)value) { }
            
        private ReactionRule(ndmf.BuildContext context, TargetProp key, GameObject controllingObject, object value)
        {
            var asc = context?.Extension<AnimationServicesContext>();
                
            TargetProp = key;

            var conditions = new List<ControlCondition>();

            var cursor = controllingObject?.transform;

            bool did_mami = false;

            while (cursor != null && !RuntimeUtil.IsAvatarRoot(cursor))
            {
                // Only look at the menu item closest to the object we're directly attached to, to avoid submenus
                // causing issues...
                var mami = cursor?.GetComponent<ModularAvatarMenuItem>();
                if (mami != null && !did_mami)
                {
                    did_mami = true;
                    
                    var mami_condition = ParameterAssignerPass.AssignMenuItemParameter(mami);
                    if (mami_condition != null) conditions.Add(mami_condition);
                }
                
                conditions.Add(new ControlCondition
                {
                    Parameter = asc?.GetActiveSelfProxy(cursor.gameObject) ?? RuntimeUtil.AvatarRootPath(cursor.gameObject),
                    DebugName = cursor.gameObject.name,
                    IsConstant = false,
                    InitialValue = cursor.gameObject.activeSelf ? 1.0f : 0.0f,
                    ParameterValueLo = 0.5f,
                    ParameterValueHi = float.PositiveInfinity,
                    ReferenceObject = cursor.gameObject
                });

                cursor = cursor.parent;
            }

            ControllingConditions = conditions;
                
            Value = value;
        }

        public TargetProp TargetProp;
        public object Value;

        public readonly List<ControlCondition> ControllingConditions;

        public bool InitiallyActive =>
            ((ControllingConditions.Count == 0) || ControllingConditions.All(c => c.InitiallyActive)) ^ Inverted;
        public bool IsDelete;

        public bool Inverted;

        public bool IsConstant => ControllingConditions.Count == 0 || ControllingConditions.All(c => c.IsConstant);
        public bool IsConstantOn => IsConstant && InitiallyActive;

        public override string ToString()
        {
            return $"AGK: {TargetProp}={Value}";
        }

        public bool TryMerge(ReactionRule other)
        {
            if (!TargetProp.Equals(other.TargetProp)) return false;
                
            // Value checks
            if (Value == other.Value) { /* objects match */ }
            else if (Value is float a && other.Value is float b)
            {
                if (Mathf.Abs(a - b) > 0.001f) return false;
            }
            else return false;
            if (!ControllingConditions.SequenceEqual(other.ControllingConditions)) return false;
            if (IsDelete || other.IsDelete) return false;

            return true;
        }
    }
}