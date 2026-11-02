#nullable enable
#if MA_VRCSDK3_AVATARS
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuItemPreviewCondition
    {
        private readonly ComputeContext _context;
        private readonly ParameterInfo _info;
        private readonly Dictionary<GameObject, ImmutableDictionary<string, float?>> _maDefaults = new();
        private readonly Dictionary<ModularAvatarMenuItem, string> _parameterNames = new();
        private readonly Dictionary<GameObject, ILookup<string, ModularAvatarMenuItem>> _menuGroups = new();
        private readonly Dictionary<GameObject, Dictionary<string, ProvidedParameter>> _parameters = new();

        public MenuItemPreviewCondition(ComputeContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _info = ParameterInfo.ForPreview(context);
        }

        internal string ParameterName(ModularAvatarMenuItem item)
        {
            if (_parameterNames.TryGetValue(item, out var cachedName)) return cachedName;
            var name = item.PortableControl.Parameter;
            if (string.IsNullOrWhiteSpace(name)) return "___AutoProp/" + item.GetEntityId();
            var mappings = _info.GetParameterRemappingsAt(item.gameObject);
            name = mappings.TryGetValue((ParameterNamespace.Animator, name), out var mapping)
                ? mapping.ParameterName : name;
            _parameterNames[item] = name;
            return name;
        }

        public ControlCondition? ConditionForPreview(ModularAvatarMenuItem item,
            IDictionary<string, ModularAvatarMenuItem?>? overrides, Dictionary<string, float> initialStates)
        {
            _context.ObservePath(item.transform);
            var root = RuntimeUtil.FindAvatarInParents(item.transform)?.gameObject;
            if (root == null) return null;
            var name = ParameterName(item);
            if (!_menuGroups.TryGetValue(root, out var groups))
            {
                groups = _context.GetComponentsInChildren<ModularAvatarMenuItem>(root, true)
                    .Where(m => !_context.ObservePath(m.transform).TakeWhile(t => t.gameObject != root)
                        .Any(t => _context.Observe(t.gameObject, go => go.CompareTag("EditorOnly"))))
                    .Select(m => { _context.Observe(m); return m; })
                    .ToLookup(ParameterName);
                _menuGroups[root] = groups;
            }
            var entries = groups[name].Select(m => new ParameterAssignerPass.MenuAssignment.Entry(m)).ToList();
            var entry = entries.FirstOrDefault(e => e.Item == item);
            if (entry == null) return null;
            // Simulate selection on detached entries; authoring values may be placeholders or duplicates.
            ModularAvatarMenuItem? selected = null;
            var hasSelectionOverride = overrides?.TryGetValue(name, out selected) == true;
            if (hasSelectionOverride)
            {
                foreach (var candidate in entries)
                {
                    candidate.isDefault = candidate.Item == selected;
                    candidate.automaticValue = true;
                }
            }
            VRCExpressionParameters.Parameter? declared = null;
            if (!hasSelectionOverride && !item.automaticValue &&
                !string.IsNullOrWhiteSpace(item.PortableControl.Parameter))
            {
                if (!_parameters.TryGetValue(root, out var parameters))
                {
                    parameters = _info.GetParametersForObject(root)
                        .Where(p => p.Namespace == ParameterNamespace.Animator)
                        .ToDictionary(p => p.EffectiveName);
                    _parameters[root] = parameters;
                }
                parameters.TryGetValue(name, out var parameter);
                if (!_maDefaults.TryGetValue(root, out var defaults))
                {
                    defaults = RenameParametersHook.GetDefaultValuesForPreview(root, _context, _info);
                    _maDefaults[root] = defaults;
                }
                var defaultValue = defaults.TryGetValue(name, out var maDefault) && maDefault.HasValue
                    ? maDefault : parameter?.DefaultValue;
                if (defaultValue.HasValue)
                    declared = new VRCExpressionParameters.Parameter
                    {
                        name = name, defaultValue = defaultValue.Value,
                        valueType = parameter?.ParameterType == AnimatorControllerParameterType.Float
                            ? VRCExpressionParameters.ValueType.Float
                            : parameter?.ParameterType == AnimatorControllerParameterType.Bool
                                ? VRCExpressionParameters.ValueType.Bool : VRCExpressionParameters.ValueType.Int
                    };
            }
            var allocation = ParameterAssignerPass.Allocate(name, entries, declared);
            if (hasSelectionOverride)
                return new ControlCondition
                {
                    Parameter = name, DebugName = item.gameObject.name, DebugReference = item,
                    IsConstant = true, InitialValue = entry.isDefault ? 1 : 0,
                    ParameterValueLo = 0.5f, ParameterValueHi = 1.5f
                };
            var initialValue = declared != null
                ? allocation.DefaultValue : entry.isDefault ? entry.Value : -999;
            initialStates[name] = initialValue;
            return new ControlCondition
            {
                Parameter = name, DebugName = item.gameObject.name, DebugReference = item,
                IsConstant = false, InitialValue = initialValue,
                ParameterValueLo = entry.Value - 0.005f, ParameterValueHi = entry.Value + 0.005f
            };
        }
    }
}
#endif
