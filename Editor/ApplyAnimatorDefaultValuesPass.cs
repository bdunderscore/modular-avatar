#region

using System;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
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

            foreach (var layer in context.AvatarDescriptor.baseAnimationLayers
                         .Concat(context.AvatarDescriptor.specialAnimationLayers))
            {
                if (layer.isDefault || layer.animatorController == null) continue;
                
                // We should have converted anything that's not an AnimationController by now
                var controller = layer.animatorController as AnimatorController;
                if (controller == null || !context.IsTemporaryAsset(controller))
                {
                    throw new Exception("Leaked unexpected controller: " + layer.animatorController + " (type " + layer.animatorController?.GetType() + ")");
                }

                var parameters = controller.parameters;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!values.TryGetValue(parameters[i].name, out var defaultValue)) continue;

                    switch (parameters[i].type)
                    {
                        case AnimatorControllerParameterType.Bool:
                            parameters[i].defaultBool = defaultValue != 0.0f;
                            break;
                        case AnimatorControllerParameterType.Int:
                            parameters[i].defaultInt = Mathf.RoundToInt(defaultValue);
                            break;
                        case AnimatorControllerParameterType.Float:
                            parameters[i].defaultFloat = defaultValue;
                            break;
                        default:
                            continue; // unhandled type, e.g. trigger
                    }
                }
                
                controller.parameters = parameters;
            }
        }
    }
}