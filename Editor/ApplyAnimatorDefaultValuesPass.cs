#if MA_VRCSDK3_AVATARS
#region

using System;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ApplyAnimatorDefaultValuesPass : Pass<ApplyAnimatorDefaultValuesPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            if (!context.AvatarDescriptor) return;

            var values = context.GetState<DefaultValues>()?.InitialValueOverrides
                         ?? ImmutableDictionary<string, float>.Empty;

            var asc = context.Extension<AnimatorServicesContext>();

            foreach (var controller in asc.ControllerContext.GetAllControllers())
            {
                var parameters = controller.Parameters;
                foreach (var (name, parameter) in parameters)
                {
                    if (!values.TryGetValue(name, out var defaultValue)) continue;

                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Bool:
                            parameter.defaultBool = defaultValue != 0.0f;
                            break;
                        case AnimatorControllerParameterType.Int:
                            parameter.defaultInt = Mathf.RoundToInt(defaultValue);
                            break;
                        case AnimatorControllerParameterType.Float:
                            parameter.defaultFloat = defaultValue;
                            break;
                        default:
                            continue; // unhandled type, e.g. trigger
                    }

                    parameters = parameters.SetItem(name, parameter);
                }
                
                controller.Parameters = parameters;
            }
        }
    }
}
#endif