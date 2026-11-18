#if MA_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.core.editor
{
    internal static class MenuItemDefaultOverrideWarning
    {
        private static readonly PropCache<ModularAvatarMenuItem, DefaultOverride?> Cache = new(
            "MenuItemDefaultOverrideWarning",
            Compute
        );

        internal static DefaultOverride? GetOverride(ModularAvatarMenuItem item)
        {
            return item != null ? Cache.Get(ComputeContext.NullContext, item) : null;
        }

        private static DefaultOverride? Compute(ComputeContext context, ModularAvatarMenuItem item)
        {
            if (item == null) return null;

            context.ObservePath(item.transform);
            var target = context.Observe(item, menuItem => (
                menuItem.Control != null,
                menuItem.Control == null ? string.Empty : menuItem.PortableControl.Parameter,
                menuItem.automaticValue,
                menuItem.isDefault
            ));
            if (!target.Item1 || !target.Item3 || string.IsNullOrWhiteSpace(target.Item2)) return null;

            var avatar = RuntimeUtil.FindAvatarInParents(item.transform);
            if (avatar == null) return null;

            var targetName = GetEffectiveName(context, item.gameObject, target.Item2);
            if (string.IsNullOrWhiteSpace(targetName)) return null;

            var matchingItemCount = 0;
            foreach (var menuItem in context.GetComponentsInChildren<ModularAvatarMenuItem>(avatar.gameObject, true))
            {
                if (menuItem == null) continue;

                var menuItemState = context.Observe(menuItem, current => (
                    current.Control != null,
                    current.Control == null ? string.Empty : current.PortableControl.Parameter
                ));
                if (!menuItemState.Item1 || string.IsNullOrWhiteSpace(menuItemState.Item2)) continue;

                if (GetEffectiveName(context, menuItem.gameObject, menuItemState.Item2) != targetName) continue;

                matchingItemCount++;
                if (matchingItemCount > 1) return null;
            }

            if (matchingItemCount != 1) return null;

            var source = ParameterInfo.ForPreview(context)
                .GetParametersForObject(avatar.gameObject)
                .FirstOrDefault(parameter =>
                    parameter.Namespace == ParameterNamespace.Animator
                    && parameter.EffectiveName == targetName
                    && !parameter.IsAnimatorOnly
                    && parameter.DefaultValue.HasValue
                    && (parameter.ParameterType == AnimatorControllerParameterType.Bool
                        || parameter.ParameterType == AnimatorControllerParameterType.Float)
                );
            if (source == null) return null;

            var newDefaultValue = target.Item4 ? 1f : 0f;
            if (Mathf.Approximately(source.DefaultValue.Value, newDefaultValue)) return null;

            return new DefaultOverride(
                targetName,
                source.DefaultValue.Value,
                newDefaultValue
            );
        }

        private static string GetEffectiveName(ComputeContext context, GameObject gameObject, string parameterName)
        {
            var mappings = ParameterIntrospectionCache.GetParameterRemappingsAt(context, gameObject);
            return mappings.TryGetValue((ParameterNamespace.Animator, parameterName), out var mapping)
                ? mapping.ParameterName
                : parameterName;
        }

        internal readonly struct DefaultOverride
        {
            internal string ParameterName { get; }
            internal float PreviousDefaultValue { get; }
            internal float NewDefaultValue { get; }

            internal DefaultOverride(string parameterName, float previousDefaultValue, float newDefaultValue)
            {
                ParameterName = parameterName;
                PreviousDefaultValue = previousDefaultValue;
                NewDefaultValue = newDefaultValue;
            }
        }
    }
}
#endif