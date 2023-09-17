using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animation;
using nadena.dev.ndmf.fluent;
using UnityEngine;

[assembly: ExportsPlugin(
    typeof(nadena.dev.modular_avatar.core.editor.plugin.PluginDefinition)
)]

namespace nadena.dev.modular_avatar.core.editor.plugin
{
    class PluginDefinition : Plugin<PluginDefinition>
    {
        public override string QualifiedName => "nadena.dev.modular-avatar";
        public override string DisplayName => "Modular Avatar";

        protected override void Configure()
        {
            Sequence seq = InPhase(BuildPhase.Resolving);
            seq.Run(ResolveObjectReferences.Instance);
            // Protect against accidental destructive edits by cloning the input controllers ASAP
            seq.Run("Clone animators", AnimationUtil.CloneAllControllers);

            seq = InPhase(BuildPhase.Transforming);
            seq.WithRequiredExtension(typeof(ModularAvatarContext), _s1 =>
            {
                seq.Run(ClearEditorOnlyTags.Instance);
                seq.Run(MeshSettingsPluginPass.Instance);
                seq.Run(RenameParametersPluginPass.Instance);
                seq.Run(MergeAnimatorPluginPass.Instance);
                seq.Run(MenuInstallPluginPass.Instance);
                seq.WithRequiredExtension(typeof(TrackObjectRenamesContext), _s2 =>
                {
                    seq.Run(MergeArmaturePluginPass.Instance);
                    seq.Run(BoneProxyPluginPass.Instance);
                    seq.Run(VisibleHeadAccessoryPluginPass.Instance);
                    seq.Run(ReplaceObjectPluginPass.Instance);
                });
                seq.Run(BlendshapeSyncAnimationPluginPass.Instance);
                seq.Run(PhysbonesBlockerPluginPass.Instance);
                ;
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
                    UnityEngine.Object.DestroyImmediate(component);
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

    class MergeArmaturePluginPass : MAPass<MergeArmaturePluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            // The animation database is currently only used by the merge armature hook; it should probably become
            // an extension context instead.
            MAContext(context).AnimationDatabase.Bootstrap(context.AvatarDescriptor);
            new MergeArmatureHook().OnPreprocessAvatar(context, context.AvatarRootObject);
            MAContext(context).AnimationDatabase.Commit();
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
            new VisibleHeadAccessoryProcessor(context.AvatarDescriptor).Process(MAContext(context));
        }
    }

    class ReplaceObjectPluginPass : MAPass<ReplaceObjectPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new ReplaceObjectPass(context).Process();
        }
    }

    class BlendshapeSyncAnimationPluginPass : MAPass<BlendshapeSyncAnimationPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new BlendshapeSyncAnimationProcessor().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class PhysbonesBlockerPluginPass : MAPass<PhysbonesBlockerPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            PhysboneBlockerPass.Process(context.AvatarRootObject);
        }
    }

    class GCGameObjectsPluginPass : MAPass<GCGameObjectsPluginPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            new GCGameObjectsPass(MAContext(context), context.AvatarRootObject).OnPreprocessAvatar();
        }
    }
}