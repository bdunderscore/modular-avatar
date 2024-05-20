#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.modular_avatar.core.editor.ScaleAdjuster;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.rq;
using nadena.dev.ndmf.rq.unity.editor;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.modular_avatar.core.editor
{
    internal class ScaleAdjusterPreview : IRenderFilter
    {
        private ScaleAdjustedBones _bones = new ScaleAdjustedBones();
        
        [InitializeOnLoadMethod]
        private static void StaticInit()
        {
        }

        private static GameObject FindAvatarRootObserving(ComputeContext ctx, GameObject ptr)
        {
            while (ptr != null)
            {
                ctx.Observe(ptr);
                var xform = ptr.transform;
                if (RuntimeUtil.IsAvatarRoot(xform)) return ptr;

                ptr = xform.parent?.gameObject;
            }

            return null;
        }

        public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups { get; } =
            ReactiveValue<IImmutableList<IImmutableList<Renderer>>>.Create(
            "Scale Adjuster: Find targets",
            async ctx =>
            {
                var scaleAdjusters = await ctx.Observe(CommonQueries.GetComponentsByType<ModularAvatarScaleAdjuster>());

                HashSet<Renderer> targets = new HashSet<Renderer>();

                foreach (var adjuster in scaleAdjusters)
                {
                    // Find parent object
                    // TODO: Reactive helper
                    var root = FindAvatarRootObserving(ctx, adjuster.gameObject);
                    if (root == null) continue;

                    var renderers = ctx.GetComponentsInChildren<Renderer>(root, true);

                    foreach (var renderer in renderers)
                    {
                        targets.Add(renderer);
                    }
                }

                return targets.Select(r => (IImmutableList<Renderer>)ImmutableList.Create(r)).ToImmutableList();
            });

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (proxy is SkinnedMeshRenderer p_smr && original is SkinnedMeshRenderer o_smr)
            {
                p_smr.rootBone = _bones.GetBone(o_smr.rootBone)?.proxy ?? o_smr.rootBone;
                p_smr.bones = o_smr.bones.Select(b =>
                {
                    var sa = (Component)b?.GetComponent<ModularAvatarScaleAdjuster>();
                    return _bones.GetBone(sa ?? b, true)?.proxy ?? b;
                }).ToArray();
            }

            _bones.Update();
        }
    }

}