using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.animation;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal partial class ReactiveObjectPass
    {
        private class ReactionData
        {
            public ReactionData(ndmf.BuildContext context, TargetProp key, GameObject controllingObject, float value)
            {
                var asc = context.Extension<AnimationServicesContext>();
                
                TargetProp = key;

                var conditions = new List<ControlCondition>();

                var cursor = controllingObject?.transform;

                // Only look at the menu item we're directly attached to, to avoid submenus causing issues...
                var mami = cursor?.GetComponent<ModularAvatarMenuItem>();
                if (mami != null)
                {
                    var mami_condition = ParameterAssignerPass.AssignMenuItemParameter(context, mami);
                    if (mami_condition != null) conditions.Add(mami_condition);
                }

                while (cursor != null && !RuntimeUtil.IsAvatarRoot(cursor))
                {
                    conditions.Add(new ControlCondition
                    {
                        Parameter = asc.GetActiveSelfProxy(cursor.gameObject),
                        DebugName = cursor.gameObject.name,
                        IsConstant = false,
                        InitialValue = cursor.gameObject.activeSelf ? 1.0f : 0.0f,
                        ParameterValueLo = 0.5f,
                        ParameterValueHi = 1.5f,
                        ReferenceObject = cursor.gameObject
                    });

                    cursor = cursor.parent;
                }

                ControllingConditions = conditions;
                
                Value = value;
            }

            public TargetProp TargetProp;
            public float Value;
            public UnityEngine.Object ObjectValue;

            public readonly List<ControlCondition> ControllingConditions;

            public bool InitiallyActive =>
                ControllingConditions.Count == 0 || ControllingConditions.All(c => c.InitiallyActive);
            public bool IsDelete;

            public bool IsConstant => ControllingConditions.Count == 0 || ControllingConditions.All(c => c.IsConstant);
            public bool IsConstantOn => IsConstant && InitiallyActive;

            public override string ToString()
            {
                return $"AGK: {TargetProp}={Value}";
            }

            public bool TryMerge(ReactionData other)
            {
                if (!TargetProp.Equals(other.TargetProp)) return false;
                if (Mathf.Abs(Value - other.Value) > 0.001f) return false;
                if (ObjectValue != other.ObjectValue) return false;
                if (!ControllingConditions.SequenceEqual(other.ControllingConditions)) return false;
                if (IsDelete || other.IsDelete) return false;

                return true;
            }
        }
    }
}