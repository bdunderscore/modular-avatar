using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animation;
using UnityEngine;

[assembly: ExportsPlugin(
    typeof(nadena.dev.modular_avatar.core.editor.plugin.PluginDefinition)
)]

namespace nadena.dev.modular_avatar.core.editor.plugin
{
    class PluginDefinition : Plugin
    {
        public override string QualifiedName => "nadena.dev.modular-avatar";

        public override ImmutableList<PluginPass> Passes => (new List<PluginPass>()
        {
            new ResolveObjectReferences(),
            new ClearEditorOnlyTags(),
            new MeshSettingsPluginPass(),
            new RenameParametersPluginPass(),
            new MergeAnimatorPluginPass(),
            new MenuInstallPluginPass(),
            new MergeArmaturePluginPass(),
            new BoneProxyPluginPass(),
            new VisibleHeadAccessoryPluginPass(),
            new ReplaceObjectPluginPass(),
            new BlendshapeSyncAnimationPluginPass(),
            new PhysbonesBlockerPluginPass(),
            new GCGameObjectsPluginPass(),
        }).ToImmutableList();
    }

    /// <summary>
    /// This plugin runs very early in order to resolve all AvatarObjectReferences to their
    /// referent before any other plugins perform heirarchy manipulations.
    /// </summary>
    internal class ResolveObjectReferences : PluginPass
    {
        public override BuiltInPhase ExecutionPhase => BuiltInPhase.Resolving;

        public override void Process(ndmf.BuildContext context)
        {
            foreach (var obj in context.AvatarRootObject.GetComponentsInChildren<AvatarTagComponent>())
            {
                obj.ResolveReferences();
            }
        }
    }

    abstract class MAPass : PluginPass
    {
        public override IImmutableSet<Type> RequiredContexts =>
            ImmutableHashSet<Type>.Empty.Add(typeof(ModularAvatarContext));

        public override IImmutableSet<object> CompatibleContexts =>
            ImmutableHashSet<object>.Empty.Add(typeof(TrackObjectRenamesContext));

        protected BuildContext MAContext(ndmf.BuildContext context)
        {
            return context.Extension<ModularAvatarContext>().BuildContext;
        }
    }

    class ClearEditorOnlyTags : MAPass
    {
        public override void Process(ndmf.BuildContext context)
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

    class MeshSettingsPluginPass : MAPass
    {
        public override void Process(ndmf.BuildContext context)
        {
            new MeshSettingsPass(MAContext(context)).OnPreprocessAvatar();
        }
    }

    class RenameParametersPluginPass : MAPass
    {
        public override void Process(ndmf.BuildContext context)
        {
            new RenameParametersHook().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class MergeAnimatorPluginPass : MAPass
    {
        public override void Process(ndmf.BuildContext context)
        {
            new MergeAnimatorProcessor().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class MenuInstallPluginPass : MAPass
    {
        public override void Process(ndmf.BuildContext context)
        {
            new MenuInstallHook().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class MergeArmaturePluginPass : MAPass
    {
        public override IImmutableSet<Type> RequiredContexts =>
            base.RequiredContexts.Add(typeof(TrackObjectRenamesContext));

        public override void Process(ndmf.BuildContext context)
        {
            // The animation database is currently only used by the merge armature hook; it should probably become
            // an extension context instead.
            MAContext(context).AnimationDatabase.Bootstrap(context.AvatarDescriptor);
            new MergeArmatureHook().OnPreprocessAvatar(context, context.AvatarRootObject);
            MAContext(context).AnimationDatabase.Commit();
        }
    }

    class BoneProxyPluginPass : MAPass
    {
        public override IImmutableSet<Type> RequiredContexts =>
            base.RequiredContexts.Add(typeof(TrackObjectRenamesContext));

        public override void Process(ndmf.BuildContext context)
        {
            new BoneProxyProcessor().OnPreprocessAvatar(context.AvatarRootObject);
        }
    }

    class VisibleHeadAccessoryPluginPass : MAPass
    {
        public override IImmutableSet<Type> RequiredContexts =>
            base.RequiredContexts.Add(typeof(TrackObjectRenamesContext));

        public override void Process(ndmf.BuildContext context)
        {
            new VisibleHeadAccessoryProcessor(context.AvatarDescriptor).Process(MAContext(context));
        }
    }

    class ReplaceObjectPluginPass : MAPass
    {
        public override IImmutableSet<Type> RequiredContexts =>
            base.RequiredContexts.Add(typeof(TrackObjectRenamesContext));

        public override void Process(ndmf.BuildContext context)
        {
            new ReplaceObjectPass(context).Process();
        }
    }

    class BlendshapeSyncAnimationPluginPass : MAPass
    {
        // Flush animation path remappings, since we need an up-to-date path name while adjusting blendshape animations
        public override IImmutableSet<object> CompatibleContexts =>
            ImmutableHashSet<object>.Empty;

        public override void Process(ndmf.BuildContext context)
        {
            new BlendshapeSyncAnimationProcessor().OnPreprocessAvatar(context.AvatarRootObject, MAContext(context));
        }
    }

    class PhysbonesBlockerPluginPass : MAPass
    {
        public override void Process(ndmf.BuildContext context)
        {
            PhysboneBlockerPass.Process(context.AvatarRootObject);
        }
    }

    class GCGameObjectsPluginPass : MAPass
    {
        public override BuiltInPhase ExecutionPhase => BuiltInPhase.Optimization;

        public override void Process(ndmf.BuildContext context)
        {
            new GCGameObjectsPass(MAContext(context), context.AvatarRootObject).OnPreprocessAvatar();
        }
    }
}