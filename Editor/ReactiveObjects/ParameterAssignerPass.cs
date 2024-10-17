using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Creates/allocates parameters to any Menu Items that need them.
    /// </summary>
    internal class ParameterAssignerPass : Pass<ParameterAssignerPass>
    {
        internal static bool ShouldAssignParametersToMami(ModularAvatarMenuItem item)
        {
            switch (item?.Control?.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    // ok
                    break;
                default:
                    return false;
            }
            
            foreach (var rc in item.GetComponentsInChildren<ReactiveComponent>(true))
            {
                // Only track components where this is the closest parent
                if (rc.transform == item.transform)
                {
                    return true;
                }
                
                var parentMami = rc.GetComponentInParent<ModularAvatarMenuItem>();
                if (parentMami == item)
                {
                    return true;
                }
            }
            
            return false;
        }

        internal void TestExecute(ndmf.BuildContext context)
        {
            Execute(context);
        }
        
        protected override void Execute(ndmf.BuildContext context)
        {
            if (!context.AvatarDescriptor) return;
            
            var paramIndex = 0;

            var declaredParams = context.AvatarDescriptor.expressionParameters.parameters
                .GroupBy(p => p.name).Select(l => l.First())
                .ToDictionary(p => p.name);

            Dictionary<string, VRCExpressionParameters.Parameter> newParameters = new();
            Dictionary<string, int> nextParamValue = new();

            Dictionary<string, List<ModularAvatarMenuItem>> _mamiByParam = new();
            foreach (var mami in context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                if (string.IsNullOrWhiteSpace(mami.Control?.parameter?.name))
                {
                    if (!ShouldAssignParametersToMami(mami)) continue;
                    
                    if (mami.Control == null) mami.Control = new VRCExpressionsMenu.Control();
                    mami.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = $"__MA/AutoParam/{mami.gameObject.name}${paramIndex++}"
                    };
                }
                
                var paramName = mami.Control.parameter.name;

                if (!_mamiByParam.TryGetValue(paramName, out var mamiList))
                {
                    mamiList = new List<ModularAvatarMenuItem>();
                    _mamiByParam[paramName] = mamiList;
                }

                mamiList.Add(mami);
            }

            foreach (var (paramName, list) in _mamiByParam)
            {
                // Assign automatic values first
                int? defaultValue = null;
                if (declaredParams.TryGetValue(paramName, out var p))
                {
                    defaultValue = (int) p.defaultValue;
                }
                else
                {
                    var floatDefault = list.FirstOrDefault(m => m.isDefault && !m.automaticValue)?.Control?.value;
                    if (floatDefault.HasValue) defaultValue = (int) floatDefault.Value;

                    if (list.Count == 1 && list[0].isDefault && list[0].automaticValue)
                        // If we have only a single entry, it's probably an on-off toggle, so we'll implicitly let 1
                        // be the 'selected' default value (if this is default and automatic value)
                        defaultValue = 1;
                }

                HashSet<int> usedValues = new();
                if (defaultValue.HasValue) usedValues.Add(defaultValue.Value);

                foreach (var item in list)
                {
                    if (!item.automaticValue)
                    {
                        usedValues.Add((int)item.Control.value);
                    }
                }

                if (!defaultValue.HasValue)
                {
                    for (int i = 0; i < 256; i++)
                    {
                        if (!usedValues.Contains(i))
                        {
                            defaultValue = i;
                            usedValues.Add(i);
                            break;
                        }
                    }
                }

                var nextValue = 1;

                var valueType = VRCExpressionParameters.ValueType.Bool;
                var isSaved = false;
                var isSynced = false;

                foreach (var mami in list)
                {
                    if (mami.automaticValue)
                    {
                        if (mami.isDefault)
                        {
                            mami.Control.value = defaultValue.GetValueOrDefault();
                        }
                        else
                        {
                            while (usedValues.Contains(nextValue)) nextValue++;

                            mami.Control.value = nextValue;
                            usedValues.Add(nextValue);
                        }
                    }

                    var newValueType = mami.ExpressionParametersValueType;
                    if (valueType == VRCExpressionParameters.ValueType.Bool || newValueType == VRCExpressionParameters.ValueType.Float)
                    {
                        valueType = newValueType;
                    }

                    isSaved |= mami.isSaved;
                    isSynced |= mami.isSynced;
                }

                if (!declaredParams.ContainsKey(paramName))
                {
                    var newParam = new VRCExpressionParameters.Parameter
                    {
                        name = paramName,
                        valueType = valueType,
                        saved = isSaved,
                        defaultValue = defaultValue.GetValueOrDefault(),
                        networkSynced = isSynced
                    };
                    newParameters[paramName] = newParam;
                }
            }

            if (newParameters.Count > 0)
            {
                var expParams = context.AvatarDescriptor.expressionParameters;
                if (!context.IsTemporaryAsset(expParams))
                {
                    expParams = Object.Instantiate(expParams);
                    context.AvatarDescriptor.expressionParameters = expParams;
                }

                expParams.parameters = expParams.parameters.Concat(newParameters.Values).ToArray();
            }
        }

        internal static ControlCondition AssignMenuItemParameter(
            ModularAvatarMenuItem mami,
            Dictionary<string, float> simulationInitialStates = null,
            IDictionary<string, ModularAvatarMenuItem> isDefaultOverrides = null,
            bool? forceSimulation = null
            )
        {
            var isSimulation = (simulationInitialStates != null || forceSimulation == true);
            
            var paramName = mami?.Control?.parameter?.name;
            if (mami?.Control != null && isSimulation && ShouldAssignParametersToMami(mami))
            {
                paramName = mami.Control?.parameter?.name;
                if (string.IsNullOrEmpty(paramName)) paramName = "___AutoProp/" + mami.GetInstanceID();

                if (simulationInitialStates != null)
                {
                    var isDefault = mami.isDefault;
                    if (isDefaultOverrides?.TryGetValue(paramName, out var target) == true)
                        isDefault = ReferenceEquals(mami, target);

                    if (isDefault)
                    {
                        simulationInitialStates[paramName] = mami.Control.value;
                    }
                    else
                    {
                        simulationInitialStates.TryAdd(paramName, -999);
                    }
                }
            }
            
            if (string.IsNullOrWhiteSpace(paramName)) return null;
            
            return new ControlCondition
            {
                Parameter = paramName,
                DebugName = mami.gameObject.name,
                IsConstant = false,
                // Note: This slightly odd-looking value is key to making the Auto checkbox work for editor previews;
                // we basically force-disable any conditions for nonselected menu items and force-enable any for default
                // menu items.
                InitialValue = mami.isDefault ? mami.Control.value : -999,
                ParameterValueLo = mami.Control.value - 0.005f,
                ParameterValueHi = mami.Control.value + 0.005f,
                DebugReference = mami,
            };
        }
    }
}