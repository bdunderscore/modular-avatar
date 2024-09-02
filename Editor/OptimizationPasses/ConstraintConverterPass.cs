using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
#if MA_VRCSDK3_AVATARS_3_7_0_OR_NEWER
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars;
using System.Linq;
using nadena.dev.modular_avatar.animation;
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
                .ToHashSet(new ObjectIdentityComparer<GameObject>());
            if (converters.Count == 0) return;

            var constraintGameObjects = context.AvatarRootObject.GetComponentsInChildren<IConstraint>(true)
                .Select(c => (c as Component)?.gameObject)
                .Distinct()
                .Where(go => go.GetComponentsInParent<ModularAvatarConvertConstraints>(true)
                    .Select(c => c.gameObject)
                    .Any(converters.Contains)
                ).ToArray();
            var targetConstraintComponents =
                constraintGameObjects.SelectMany(go => go.GetComponents<IConstraint>()).ToArray();

            AvatarDynamicsSetup.DoConvertUnityConstraints(targetConstraintComponents, null, false);

            var asc = context.Extension<AnimationServicesContext>();

            // Also look for preexisting VRCConstraints so we can go fix up any broken animation clips from people who
            // clicked auto fix :(
            var existingVRCConstraints = converters.SelectMany(c => c.GetComponentsInChildren<VRCConstraintBase>(true))
                .Select(c => c.gameObject)
                .Distinct();

            var targetPaths = constraintGameObjects
                .Union(existingVRCConstraints)
                .Select(c => asc.PathMappings.GetObjectIdentifier(c))
                .ToHashSet();

            // Update animation clips
            var clips = targetPaths.SelectMany(tp => asc.AnimationDatabase.ClipsForPath(tp))
                .ToHashSet();

            foreach (var clip in clips) RemapSingleClip(clip, targetPaths);
        }

        private void RemapSingleClip(AnimationDatabase.ClipHolder clip, HashSet<string> targetPaths)
        {
            var motion = clip.CurrentClip as AnimationClip;
            if (motion == null) return;

            var bindings = AnimationUtility.GetCurveBindings(motion);
            var toUpdateBindings = new List<EditorCurveBinding>();
            var toUpdateCurves = new List<AnimationCurve>();

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
                        var curve = AnimationUtility.GetEditorCurve(motion, ecb);
                        if (curve != null)
                        {
                            toUpdateBindings.Add(newBinding);
                            toUpdateCurves.Add(curve);

                            toUpdateBindings.Add(ecb);
                            toUpdateCurves.Add(null);
                        }
                    }
            }

            if (toUpdateBindings.Count == 0) return;
            AnimationUtility.SetEditorCurves(motion, toUpdateBindings.ToArray(), toUpdateCurves.ToArray());
        }

#else
        protected override void Execute(ndmf.BuildContext context) {}
#endif
    }
}