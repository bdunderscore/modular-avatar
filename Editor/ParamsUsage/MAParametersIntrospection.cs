#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.plugin;
using nadena.dev.ndmf;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    [ParameterProviderFor(typeof(ModularAvatarMenuItem))]
    internal class MAMenuItemIntrospection : IParameterProvider
    {
        private readonly ModularAvatarMenuItem _component;

        public MAMenuItemIntrospection(ModularAvatarMenuItem menuItem)
        {
            _component = menuItem;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(ndmf.BuildContext context = null)
        {
            if (_component.Control == null) yield break;
            if (!ParameterAssignerPass.ShouldAssignParametersToMami(_component)) yield break;

            var hidden = false;
            var name = _component.Control?.parameter?.name;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"__MA/AutoParam/{_component.gameObject.name}${_component.GetInstanceID()}";
                hidden = true;
            }

            yield return new ProvidedParameter(
                name,
                ParameterNamespace.Animator,
                _component, PluginDefinition.Instance, _component.AnimatorControllerParameterType)
            {
                ExpandTypeOnConflict = true,
                WantSynced = _component.isSynced,
                IsHidden = hidden,
                DefaultValue = _component.isDefault ? _component.Control.value : null
            };
        }
    }
    
    [ParameterProviderFor(typeof(ModularAvatarParameters))]
    internal class MAParametersIntrospection : IParameterProvider
    {
        private readonly ModularAvatarParameters _component;

        public MAParametersIntrospection(ModularAvatarParameters parameters)
        {
            _component = parameters;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(ndmf.BuildContext context = null)
        {
            return _component.parameters.Select(p =>
            {
                AnimatorControllerParameterType paramType;
                bool animatorOnly = false;

                switch (p.syncType)
                {
                    case ParameterSyncType.Bool:
                        paramType = AnimatorControllerParameterType.Bool;
                        break;
                    case ParameterSyncType.Float:
                        paramType = AnimatorControllerParameterType.Float;
                        break;
                    case ParameterSyncType.Int:
                        paramType = AnimatorControllerParameterType.Int;
                        break;
                    default:
                        paramType = AnimatorControllerParameterType.Float;
                        animatorOnly = true;
                        break;
                }

                return new ProvidedParameter(
                    p.nameOrPrefix,
                    p.isPrefix ? ParameterNamespace.PhysBonesPrefix : ParameterNamespace.Animator,
                    _component, PluginDefinition.Instance, paramType)
                {
                    IsAnimatorOnly = animatorOnly,
                    WantSynced = !p.localOnly,
                    IsHidden = p.internalParameter,
                    DefaultValue = p.defaultValue
                };
            });
        }

        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap,
            ndmf.BuildContext context = null)
        {
            var remappings = context != null ? ParameterRenameMappings.Get(context) : null;
            
            // TODO - internal parameter handling
            foreach (var p in _component.parameters)
            {
                ParameterNamespace ns = p.isPrefix ? ParameterNamespace.PhysBonesPrefix : ParameterNamespace.Animator;
                string remapTo = null;
                if (p.internalParameter)
                {
                    if (remappings != null)
                    {
                        remapTo = remappings.Remap(_component, ns, p.nameOrPrefix);
                    }
                    else
                    {
                        remapTo = p.nameOrPrefix + "$" + _component.GetInstanceID();
                    }
                }
                else if (string.IsNullOrEmpty(p.remapTo))
                {
                    continue;
                }
                else
                {
                    remapTo = p.remapTo;
                }

                if (nameMap.TryGetValue((ns, remapTo), out var existingMapping))
                {
                    remapTo = existingMapping.ParameterName;
                }

                nameMap = nameMap.SetItem((ns, p.nameOrPrefix), new ParameterMapping(remapTo, p.internalParameter));
            }
        }
    }
}