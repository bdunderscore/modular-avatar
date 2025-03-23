#nullable enable

using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
#if MA_VRCSDK3_AVATARS_3_7_0_OR_NEWER
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars;
using System.Linq;
using nadena.dev.ndmf.animator;
using VRC.Dynamics;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ConstraintConverterPass : Pass<ConstraintConverterPass>
    {
#if MA_VRCSDK3_AVATARS_3_7_0_OR_NEWER
        [InitializeOnLoadMethod]
        private static void Init()
        {
            AvatarDynamicsSetup.IsUnityConstraintAutoConverted += constraint =>
            {
                var component = constraint as Component;
                if (component == null) return false;

                var converted = component.GetComponentInParent<ModularAvatarConvertConstraints>();

                return converted != null && RuntimeUtil.FindAvatarInParents(converted.transform) ==
                    RuntimeUtil.FindAvatarInParents(component.transform);
            };

            AvatarDynamicsSetup.OnConvertUnityConstraintsAcrossGameObjects += (constraints, isAutoFix) =>
            {
                if (!isAutoFix) return false;
                
                var avatars = constraints.Select(c => RuntimeUtil.FindAvatarInParents(c.transform)).Distinct();

                foreach (var avatar in avatars) Undo.AddComponent<ModularAvatarConvertConstraints>(avatar.gameObject);

                return true;
            };
        }

        protected override void Execute(ndmf.BuildContext context)
        {
            var converters = context.AvatarRootObject.GetComponentsInChildren<ModularAvatarConvertConstraints>(true)
                .Select(c => c.gameObject)
                .ToHashSet();
            if (converters.Count == 0) return;

            var constraintGameObjects = context.AvatarRootObject.GetComponentsInChildren<IConstraint>(true)
                .Select(c => (c as Component)?.gameObject)
                .Where(go => go != null)
                .Distinct()
                .Where(go => go!.GetComponentsInParent<ModularAvatarConvertConstraints>(true)
                    .Select(c => c.gameObject)
                    .Any(converters.Contains)
                ).ToArray();
            var targetConstraintComponents =
                constraintGameObjects.SelectMany(go => go!.GetComponents<IConstraint>()).ToArray();

            AvatarDynamicsSetup.DoConvertUnityConstraints(targetConstraintComponents, null, false);

            var asc = context.Extension<AnimatorServicesContext>();

            // Also look for preexisting VRCConstraints so we can go fix up any broken animation clips from people who
            // clicked auto fix :(
            var existingVRCConstraints = converters.SelectMany(c => c.GetComponentsInChildren<VRCConstraintBase>(true))
                .Select(c => c.gameObject)
                .Distinct();

            var targetPaths = constraintGameObjects
                .Union(existingVRCConstraints)
                .Select(c => asc.ObjectPathRemapper.GetVirtualPathForObject(c!))
                .ToHashSet();

            // Update animation clips

            var clips = targetPaths.SelectMany(tp => asc.AnimationIndex.GetClipsForObjectPath(tp))
                .ToHashSet();

            foreach (var clip in clips) RemapSingleClip(clip, targetPaths);
        }

        private void RemapSingleClip(VirtualClip clip, HashSet<string> targetPaths)
        {
            var bindings = clip.GetFloatCurveBindings().ToList();

            foreach (var ecb in bindings)
            {
                if (!targetPaths.Contains(ecb.path)) continue;
                if (typeof(IConstraint).IsAssignableFrom(ecb.type))
                    if (AvatarDynamicsSetup.TryGetSubstituteAnimationBinding(ecb.type, ecb.propertyName,
                            out var newType, out var newProp, out var isArray))
                    {
                        var newBinding = new EditorCurveBinding
                        {
                            path = ecb.path,
                            type = newType,
                            propertyName = newProp
                        };
                        var curve = clip.GetFloatCurve(ecb);
                        clip.SetFloatCurve(newBinding, curve);
                        clip.SetFloatCurve(ecb, null);
                    }
            }
        }

#else
        protected override void Execute(ndmf.BuildContext context) {}
#endif
    }
}