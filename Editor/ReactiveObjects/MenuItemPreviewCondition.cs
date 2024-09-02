using System;
using System.Collections.Generic;
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
        private readonly Dictionary<GameObject, Dictionary<string, ProvidedParameter>> _registeredParameters = new();

        public MenuItemPreviewCondition(ComputeContext computeContext)
        {
            if (computeContext == null) throw new ArgumentNullException(nameof(computeContext));
            _info = ParameterInfo.ForPreview(computeContext);
            _context = computeContext;
        }

        private Dictionary<string, ProvidedParameter> RegisteredParameters(GameObject obj)
        {
            _context.ObservePath(obj.transform);

            var root = RuntimeUtil.FindAvatarInParents(obj.transform)?.gameObject;
            if (root == null) return new Dictionary<string, ProvidedParameter>();

            if (_registeredParameters.TryGetValue(root, out var parameters))
                return parameters;

            parameters = new Dictionary<string, ProvidedParameter>();

            foreach (var param in _info.GetParametersForObject(root)) parameters[param.EffectiveName] = param;

            _registeredParameters[root] = parameters;
            return parameters;
        }

        private bool TryGetRegisteredParam(ModularAvatarMenuItem mami, string paramName,
            out ProvidedParameter providedParameter)
        {
            providedParameter = default;

            if (string.IsNullOrWhiteSpace(mami.Control?.parameter?.name)) return false;

            var remaps = _info.GetParameterRemappingsAt(mami.gameObject);

            if (remaps.TryGetValue((ParameterNamespace.Animator, paramName), out var remap))
                paramName = remap.ParameterName;

            return RegisteredParameters(mami.gameObject).TryGetValue(paramName, out providedParameter);
        }

        public bool IsEnabledForPreview(ModularAvatarMenuItem mami)
        {
            _context.ObservePath(mami.transform);
            if (_context.Observe(mami, _ => mami.Control == null)) return false;

            var (paramName, value) = _context.Observe(mami, m => (m.Control.parameter.name, m.Control.value));

            if (TryGetRegisteredParam(mami, paramName, out var providedParameter))
            {
                var defaultValue = providedParameter.DefaultValue ?? 0;
                return Mathf.Abs(defaultValue - value) < 0.01f;
            }

            return _context.Observe(mami, _ => mami.isDefault);
        }
    }
}