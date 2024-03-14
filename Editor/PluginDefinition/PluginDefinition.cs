﻿#region

using System;
using nadena.dev.modular_avatar.animation;
using nadena.dev.modular_avatar.core.ArmatureAwase;
using nadena.dev.modular_avatar.core.editor.plugin;
using nadena.dev.modular_avatar.editor.ErrorReporting;
using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

[assembly: ExportsPlugin(
    typeof(PluginDefinition)
)]

namespace nadena.dev.modular_avatar.core.editor.plugin
{
    class PluginDefinition : Plugin<PluginDefinition>
    {
        public override string QualifiedName => "nadena.dev.modular-avatar";
        public override string DisplayName => "Modular Avatar";
        public override Texture2D LogoTexture => LogoDisplay.LOGO_ASSET;

        // 00a0e9
        public override Color? ThemeColor => new Color(0x00 / 255f, 0xa0 / 255f, 0xe9 / 255f, 1);

        protected override void OnUnhandledException(Exception e)
        {
            BuildReport.LogException(e);
        }

        protected override void Configure()
        {
            Sequence seq = InPhase(BuildPhase.Resolving);
            seq.Run(ResolveObjectReferences.Instance);
            // Protect against accidental destructive edits by cloning the input controllers ASAP
            seq.Run("Clone animators", AnimationUtil.CloneAllControllers);

            seq = InPhase(BuildPhase.Transforming);
            seq.Run("Validate configuration",
                context => ComponentValidation.ValidateAll(context.AvatarRootObject));
            seq.WithRequiredExtension(typeof(ModularAvatarContext), _s1 =>
            {
                seq.Run(ClearEditorOnlyTags.Instance);
                seq.Run(MeshSettingsPluginPass.Instance);
                seq.Run(ScaleAdjusterPass.Instance);
#if MA_VRCSDK3_AVATARS
                seq.Run(RenameParametersPluginPass.Instance);
                seq.Run(MergeBlendTreePass.Instance);
                seq.Run(MergeAnimatorPluginPass.Instance);
                seq.Run(ApplyAnimatorDefaultValuesPass.Instance);
                seq.Run(MenuInstallPluginPass.Instance);
#endif
                seq.WithRequiredExtension(typeof(AnimationServicesContext), _s2 =>
                {
                    seq.Run(MergeArmaturePluginPass.Instance);
                    seq.Run(BoneProxyPluginPass.Instance);
                    seq.Run(VisibleHeadAccessoryPluginPass.Instance);
                    seq.Run("World Fixed Object",
                        ctx => new WorldFixedObjectProcessor().Process(ctx)
                    );
                    seq.Run(ReplaceObjectPluginPass.Instance);
#if MA_VRCSDK3_AVATARS
                    seq.Run(BlendshapeSyncAnimationPluginPass.Instance);
#endif
                });
#if MA_VRCSDK3_AVATARS
                seq.Run(PhysbonesBlockerPluginPass.Instance);
                seq.Run("Fixup Expressions Menu", ctx =>
                {
                    var maContext = ctx.Extension<ModularAvatarContext>().BuildContext;
                    FixupExpressionsMenuPass.FixupExpressionsMenu(maContext);
                });
#endif
                seq.Run("Rebind humanoid avatar", ctx =>
                {
                    // workaround problem with avatar matching
                    // https://github.com/bdunderscore/modular-avatar/issues/430
                    var animator = ctx.AvatarRootObject.GetComponent<Animator>();
                    if (animator)
                    {
                        var avatar = animator.avatar;
                        animator.avatar = null;
                        // ReSharper disable once Unity.InefficientPropertyAccess
                        animator.avatar = avatar;
                    }
                });
                seq.Run("Purge ModularAvatar components", ctx =>
                {
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<AvatarTagComponent>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                    foreach (var component in ctx.AvatarRootTransform.GetComponentsInChildren<MAMoveIndependently>(true))
                    {
                        Object.DestroyImmediate(component);
                    }
                });
#if MA_VRCSDK3_AVATARS
                seq.Run(PruneParametersPass.Instance);
#endif
            });

            InPhase(BuildPhase.Optimizing)
                .WithRequiredExtension(typeof(ModularAvatarContext),
                    s => s.Run(GCGameObjectsPluginPass.Instance));
        }
    }

    /// <summary>
    /// This plugin runs very early in order to resolve all AvatarObjectReferences to their
    /// referent before any other plugins perform heirarchy manipulations.
    /// </summary>
    internal class ResolveObjectReferences : Pass<ResolveObjectReferences>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            foreach (var obj in context.AvatarRootObject.GetComponentsInChildren<AvatarTagComponent>())
            {
                obj.ResolveReferences();
            }
        }
    }

    abstract class MAPass<T> : Pass<T> where T : Pass<T>, new()
    {
        protected BuildContext MAContext(ndmf.BuildContext context)
        {
            return context.Extension<ModularAvatarContext>().BuildContext;
        }
    }

    class ClearEditorOnlyTags : MAPass<ClearEditorOnlyTags>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            Traverse(context.AvatarRootTransform);
        }

        void Traverse(Transform obj)
        {
            // EditorOnly objects can be used for multiple purposes - users might want a camera rig to be available in
            // play mode, for example. For now, we'll prune MA components from EditorOnly objects, but otherwise leave
            // them in place when in play mode.

            if (obj.CompareTag("EditorOnly"))
            {
                foreach (var component in obj.GetComponentsInChildren<AvatarTagComponent>(true))
                {
                    Object.DestroyImmediate(component);
                }
            }
            else
            {
                foreach (Transform transform in obj)
                {
                    Traverse(transform);
                }
            }
        }
    }

    class MeshSettingsPluginPass : MAPass<MeshSettingsPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new MeshSettingsPass(MAContext(context)).OnPreprocessAvatar();
        }
    }

#if MA_VRCSDK3_AVATARS
    class RenameParametersPluginPass : MAPass<RenameParametersPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new RenameParametersHook().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class MergeAnimatorPluginPass : MAPass<MergeAnimatorPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new MergeAnimatorProcessor().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class MenuInstallPluginPass : MAPass<MenuInstallPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new MenuInstallHook().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }
#endif

    class MergeArmaturePluginPass : MAPass<MergeArmaturePluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new MergeArmatureHook().OnPreprocessAvatar(context, context.AvatarRootObject);
        }
    }

    class BoneProxyPluginPass : MAPass<BoneProxyPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new BoneProxyProcessor().OnPreprocessAvatar(context.AvatarRootObject);
        }
    }

    class VisibleHeadAccessoryPluginPass : MAPass<VisibleHeadAccessoryPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new VisibleHeadAccessoryProcessor(MAContext(context)).Process();
        }
    }

    class ReplaceObjectPluginPass : MAPass<ReplaceObjectPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new ReplaceObjectPass(context).Process();
        }
    }

#if MA_VRCSDK3_AVATARS
    class BlendshapeSyncAnimationPluginPass : MAPass<BlendshapeSyncAnimationPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new BlendshapeSyncAnimationProcessor().OnPreprocessAvatar(MAContext(context));
        }
    }

    class PhysbonesBlockerPluginPass : MAPass<PhysbonesBlockerPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            PhysboneBlockerPass.Process(context.AvatarRootObject);
        }
    }
#endif

    class GCGameObjectsPluginPass : MAPass<GCGameObjectsPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new GCGameObjectsPass(MAContext(context), context.AvatarRootObject).OnPreprocessAvatar();
        }
    }
}