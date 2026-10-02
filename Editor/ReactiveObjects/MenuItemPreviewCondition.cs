#nullable enable

#if MA_VRCSDK3_AVATARS
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal class MenuItemPreviewCondition
    {
        private readonly ComputeContext _context;
        private readonly ParameterInfo _info;

        // avatar root => params
        private readonly Dictionary<GameObject, Dictionary<string, float?>> _registeredParameters = new();

        public MenuItemPreviewCondition(ComputeContext computeContext)
        {
            if (computeContext == null) throw new ArgumentNullException(nameof(computeContext));
            _info = ParameterInfo.ForPreview(computeContext);
            _context = computeContext;
        }

        private Dictionary<string, float?> RegisteredParameters(GameObject obj)
        {
            _context.ObservePath(obj.transform);

            var root = RuntimeUtil.FindAvatarInParents(obj.transform)?.gameObject;
            if (root == null) return new Dictionary<string, float?>();

            if (_registeredParameters.TryGetValue(root, out var parameters))
                return parameters;

            parameters = new Dictionary<string, float?>();

            foreach (var param in _info.GetParametersForObject(root))
            {
                if (param.Namespace == ParameterNamespace.Animator)
                    parameters[param.EffectiveName] = param.DefaultValue;
            }

            // MA's explicit defaults override expression parameters. Traverse parents before children so an outer
            // explicit default wins, while an unspecified outer default can inherit an inner one.
            var explicitDefaults = new Dictionary<string, float>();
            foreach (var component in _context.GetComponentsInChildren<ModularAvatarParameters>(root, true))
            {
                if (_context.ObservePath(component.transform).TakeWhile(t => t.gameObject != root)
                    .Any(t => _context.Observe(t.gameObject, go => go.CompareTag("EditorOnly"))))
                    continue;

                var configs = _context.Observe(component, c => c.parameters.ToArray(), Enumerable.SequenceEqual);
                var remaps = _info.GetParameterRemappingsAt(component, true);
                foreach (var config in configs)
                {
                    if (config.isPrefix || !config.HasDefaultValue) continue;

                    var name = config.nameOrPrefix;
                    if (remaps.TryGetValue((ParameterNamespace.Animator, name), out var remap))
                        name = remap.ParameterName;

                    if (explicitDefaults.TryAdd(name, config.defaultValue))
                        parameters[name] = config.defaultValue;
                }
            }

            _registeredParameters[root] = parameters;
            return parameters;
        }

        private bool TryGetRegisteredParam(ModularAvatarMenuItem mami, string paramName,
            out float? defaultValue)
        {
            defaultValue = null;

            if (string.IsNullOrWhiteSpace(mami.PortableControl.Parameter)) return false;

            var remaps = _info.GetParameterRemappingsAt(mami.gameObject);

            if (remaps.TryGetValue((ParameterNamespace.Animator, paramName), out var remap))
                paramName = remap.ParameterName;

            return RegisteredParameters(mami.gameObject).TryGetValue(paramName, out defaultValue);
        }

        public float InitialValueForPreview(ModularAvatarMenuItem mami)
        {
            _context.ObservePath(mami.transform);
            var (paramName, value, automaticValue, isDefault) = _context.Observe(mami,
                m => (m.PortableControl.Parameter, m.PortableControl.Value, m.automaticValue, m.isDefault));

            if (!automaticValue && !string.IsNullOrWhiteSpace(paramName) &&
                TryGetRegisteredParam(mami, paramName, out var defaultValue) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            return isDefault ? value : -999;
        }
    }
}
#endif
