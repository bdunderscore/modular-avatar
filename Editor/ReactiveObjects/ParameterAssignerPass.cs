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
                float defaultValue;
                if (declaredParams.TryGetValue(paramName, out var p))
                {
                    defaultValue = p.defaultValue;
                }
                else
                {
                    defaultValue = list.FirstOrDefault(m => m.isDefault && !m.automaticValue)?.Control?.value ?? 0;

                    if (list.Count == 1)
                        // If we have only a single entry, it's probably an on-off toggle, so we'll implicitly let 0
                        // be the 'unselected' default value (if this is not default)
                        defaultValue = list[0].isDefault ? 1 : 0;
                }

                HashSet<int> usedValues = new();
                usedValues.Add((int)defaultValue);

                foreach (var item in list)
                    if (!item.automaticValue && Mathf.Abs(item.Control.value - Mathf.Round(item.Control.value)) < 0.01f)
                        usedValues.Add(Mathf.RoundToInt(item.Control.value));

                var nextValue = 1;

                var canBeBool = true;
                var canBeInt = true;
                var isSaved = true;
                var isSynced = true;

                foreach (var mami in list)
                {
                    if (mami.automaticValue)
                    {
                        if (list.Count == 1)
                        {
                            mami.Control.value = 1;
                        }
                        else
                        {
                            while (usedValues.Contains(nextValue)) nextValue++;

                            mami.Control.value = nextValue;
                            usedValues.Add(nextValue);
                        }
                    }

                    if (Mathf.Abs(mami.Control.value - Mathf.Round(mami.Control.value)) > 0.01f)
                        canBeInt = false;
                    else
                        canBeBool &= mami.Control.value is >= 0 and <= 1;

                    isSaved &= mami.isSaved;
                    isSynced &= mami.isSynced;
                }

                if (!declaredParams.ContainsKey(paramName))
                {
                    VRCExpressionParameters.ValueType newType;
                    if (canBeBool) newType = VRCExpressionParameters.ValueType.Bool;
                    else if (canBeInt) newType = VRCExpressionParameters.ValueType.Int;
                    else newType = VRCExpressionParameters.ValueType.Float;

                    var newParam = new VRCExpressionParameters.Parameter
                    {
                        name = paramName,
                        valueType = newType,
                        saved = isSaved,
                        defaultValue = defaultValue,
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

                var isDefault = mami.isDefault;
                if (isDefaultOverrides?.TryGetValue(paramName, out var target) == true)
                    isDefault = ReferenceEquals(mami, target);

                if (isDefault)
                {
                    simulationInitialStates[paramName] = mami.Control.value;
                }
                else
                {
                    simulationInitialStates?.TryAdd(paramName, -999);
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
                ParameterValueLo = mami.Control.value - 0.5f,
                ParameterValueHi = mami.Control.value + 0.5f,
                DebugReference = mami,
            };
        }
    }
}