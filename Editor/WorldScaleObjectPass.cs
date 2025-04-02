using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
#if MA_VRCSDK3_AVATARS
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

#else
using UnityEngine.Animations;
#endif

namespace nadena.dev.modular_avatar.core.editor
{
    internal class WorldScaleObjectPass : Pass<WorldScaleObjectPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var fixedPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Packages/nadena.dev.modular-avatar/Assets/FixedPrefab.prefab"
                );
            var targets = context.AvatarRootTransform.GetComponentsInChildren<ModularAvatarWorldScaleObject>(true);

            foreach (var target in targets)
            {
                BuildReport.ReportingObject(target, () =>
                {
#if MA_VRCSDK3_AVATARS
                    var c = target.gameObject.AddComponent<VRCScaleConstraint>();
                    c.Sources.Add(new VRCConstraintSource(fixedPrefab.transform, 1));
                    c.Locked = true;
                    c.IsActive = true;
#else
                    var c = target.gameObject.AddComponent<ScaleConstraint>();
                    c.AddSource(new ConstraintSource() {sourceTransform = fixedPrefab.transform, weight = 1});
                    c.locked = true;
                    c.constraintActive = true;
#endif

                    Object.DestroyImmediate(target);
                });
            }
        }
    }
}