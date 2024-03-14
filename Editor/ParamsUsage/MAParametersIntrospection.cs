#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.plugin;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
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
                        remapTo = p.nameOrPrefix + "$" + GUID.Generate();
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

                if (nameMap.TryGetKey((ns, remapTo), out var existingMapping))
                {
                    remapTo = existingMapping.Item2;
                }

                nameMap = nameMap.SetItem((ns, p.nameOrPrefix), new ParameterMapping(remapTo, p.internalParameter));
            }
        }
    }
}