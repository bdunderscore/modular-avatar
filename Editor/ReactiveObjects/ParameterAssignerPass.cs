#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;

#if MA_VRCSDK3_AVATARS
using BuildReport = nadena.dev.modular_avatar.editor.ErrorReporting.BuildReport;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Creates/allocates parameters to any Menu Items that need them.
    /// </summary>
    internal class ParameterAssignerPass : Pass<ParameterAssignerPass>
    {
        internal const string AUTOMATIC_PARAMETER_PREFIX = "__MA/AutoParam/";

        internal static bool ShouldAssignParametersToMami(ModularAvatarMenuItem? item)
        {
            if (item == null) return false;

            var control = item.PortableControl;
            switch (control.Type)
            {
                case PortableControlType.Button:
                case PortableControlType.Toggle:
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
            
            var mappings = ParameterRenameMappings.Get(context);

            var declaredParams = context.AvatarDescriptor.expressionParameters.parameters
                .GroupBy(p => p.name).Select(l => l.First())
                .ToDictionary(p => p.name);

            Dictionary<string, VRCExpressionParameters.Parameter> newParameters = new();

            Dictionary<string, List<ModularAvatarMenuItem>> _mamiByParam = new();
            foreach (var mami in context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                var control = mami.PortableControl;
                if (string.IsNullOrWhiteSpace(control.Parameter))
                {
                    if (!ShouldAssignParametersToMami(mami)) continue;

                    control.Parameter = mappings.Remap(mami, ParameterNamespace.Animator,
                        AUTOMATIC_PARAMETER_PREFIX + mami.gameObject.name);
                }

                var paramName = control.Parameter;
                if (string.IsNullOrWhiteSpace(paramName)) continue;

                if (!_mamiByParam.TryGetValue(paramName, out var mamiList))
                {
                    mamiList = new List<ModularAvatarMenuItem>();
                    _mamiByParam[paramName] = mamiList;
                }

                mamiList.Add(mami);
            }

            foreach (var (paramName, list) in _mamiByParam)
            {
                declaredParams.TryGetValue(paramName, out var declared);
                var allocation = Allocate(paramName, list.Select(m => new MenuAssignment.Entry(m)).ToList(), declared);
                foreach (var entry in allocation.Entries)
                {
                    entry.Item.PortableControl.Value = entry.Value;
                    entry.Item.isDefault = entry.isDefault;
                }
                if (allocation.Overflow != null)
                    BuildReport.LogFatal("error.parameter_assigner.value_out_of_range", paramName, allocation.Overflow);
                if (allocation.Parameter != null) newParameters[paramName] = allocation.Parameter;
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

        internal sealed class MenuAssignment
        {
            internal sealed class Entry
            {
                internal ModularAvatarMenuItem Item;
                internal bool automaticValue, isDefault, isSaved, isSynced;
                internal float Value;
                internal VRCExpressionParameters.ValueType ExpressionParametersValueType =>
                    Value < 0 || Mathf.Abs(Value - Mathf.Round(Value)) > 0.01f
                        ? VRCExpressionParameters.ValueType.Float
                        : Value > 1 ? VRCExpressionParameters.ValueType.Int : VRCExpressionParameters.ValueType.Bool;

                internal Entry(ModularAvatarMenuItem item)
                {
                    Item = item;
                    Value = item.PortableControl.Value;
                    automaticValue = item.automaticValue;
                    isDefault = item.isDefault;
                    isSaved = item.isSaved;
                    isSynced = item.isSynced;
                }
            }

            internal List<Entry> Entries;
            internal VRCExpressionParameters.Parameter? Parameter;
            internal ModularAvatarMenuItem? Overflow;
            internal float DefaultValue;

            internal MenuAssignment(List<Entry> entries) { Entries = entries; }
        }

        // Operates on detached snapshots so previews and builds use exactly the same allocation rules.
        internal static MenuAssignment Allocate(string paramName, List<MenuAssignment.Entry> list,
            VRCExpressionParameters.Parameter? p)
        {
            var result = new MenuAssignment(list);
            // Assign automatic values first
            int? defaultValue = null;
            if (p != null && !Mathf.Approximately(0, p.defaultValue))
            {
                defaultValue = (int) p.defaultValue;
            }
            else
            {
                var floatDefault = list.FirstOrDefault(m => m.isDefault && !m.automaticValue)?.Value;
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
                    usedValues.Add((int)item.Value);
                    item.isDefault = defaultValue.HasValue && (int)item.Value == defaultValue;
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
                var control = mami;
                if (mami.automaticValue)
                {
                    if (mami.isDefault)
                    {
                        control.Value = defaultValue.GetValueOrDefault();
                    }
                    else if (p != null && p.valueType != VRCExpressionParameters.ValueType.Int)
                    {
                        // For a float or bool value, we don't really have a lot of good choices, so just set it to
                        // 1
                        control.Value = 1;
                    }
                    else
                    {
                        while (usedValues.Contains(nextValue) && nextValue < 256) nextValue++;

                        if (nextValue >= 256)
                        {
                            result.Overflow = mami.Item;
                            break;
                        }

                        control.Value = nextValue;
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

            if (p == null)
            {
                var newParam = new VRCExpressionParameters.Parameter
                {
                    name = paramName,
                    valueType = valueType,
                    saved = isSaved,
                    defaultValue = defaultValue.GetValueOrDefault(),
                    networkSynced = isSynced
                };
                result.Parameter = newParam;
            }
            result.DefaultValue = p?.defaultValue ?? defaultValue.GetValueOrDefault();
            return result;
        }

        internal static ControlCondition? AssignMenuItemParameter(
            ModularAvatarMenuItem? mami,
            Dictionary<string, float>? simulationInitialStates = null,
            IDictionary<string, ModularAvatarMenuItem?>? isDefaultOverrides = null,
            bool? forceSimulation = null
            )
        {
            var isSimulation = (simulationInitialStates != null || forceSimulation == true);

            var control = mami?.PortableControl;
            var paramName = control?.Parameter;
            if (mami != null && control != null && isSimulation && ShouldAssignParametersToMami(mami))
            {
                var simulationParamName = control.Parameter;
                if (simulationParamName.Length == 0)
                    simulationParamName = "___AutoProp/" + mami.GetEntityId().ToString();
                paramName = simulationParamName;

                if (simulationInitialStates != null)
                {
                    var isDefault = mami.isDefault;
                    ModularAvatarMenuItem? target = null;
                    if (isDefaultOverrides?.TryGetValue(simulationParamName, out target) == true)
                        isDefault = ReferenceEquals(mami, target);

                    if (isDefault)
                    {
                        simulationInitialStates[simulationParamName] = control.Value;
                    }
                    else
                    {
                        simulationInitialStates.TryAdd(simulationParamName, -999);
                    }
                }
            }
            
            if (string.IsNullOrWhiteSpace(paramName)) return null;
            if (mami == null || control == null) return null;
            
            return new ControlCondition
            {
                Parameter = paramName,
                DebugName = mami.gameObject.name,
                IsConstant = false,
                // Note: This slightly odd-looking value is key to making the Auto checkbox work for editor previews;
                // we basically force-disable any conditions for nonselected menu items and force-enable any for default
                // menu items.
                InitialValue = mami.isDefault ? control.Value : -999,
                ParameterValueLo = control.Value - 0.005f,
                ParameterValueHi = control.Value + 0.005f,
                DebugReference = mami,
            };
        }
    }
}
#else
using System.Collections.Generic;
using nadena.dev.ndmf;

namespace nadena.dev.modular_avatar.core.editor
{
    /// <summary>
    ///     Creates/allocates parameters to any Menu Items that need them.
    ///
    ///     (non-VRChat SDK stub)
    /// </summary>
    internal class ParameterAssignerPass : Pass<ParameterAssignerPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            
        }
        
        internal void TestExecute(ndmf.BuildContext context)
        {
            Execute(context);
        }
        
        internal static bool ShouldAssignParametersToMami(ModularAvatarMenuItem? item)
        {
            return false;
        }

        internal static ControlCondition? AssignMenuItemParameter(
            ModularAvatarMenuItem? mami,
            Dictionary<string, float>? simulationInitialStates = null,
            IDictionary<string, ModularAvatarMenuItem?>? isDefaultOverrides = null,
            bool? forceSimulation = null
        )
        {
            return null;
        }
    }
}

#endif
