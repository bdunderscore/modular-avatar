#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEngine;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    internal class HideOtherAvatarsTracker
    {
        public readonly PublishedValue<Object> targetAvatar = new(null);

        public ImmutableHashSet<Renderer> GetHiddenRenderers(ComputeContext context)
        {
            var targetAvatar = context.Observe(this.targetAvatar,
                o => o as GameObject,
                (a, b) => a == b
            );

            return context.GetAvatarRoots()
                .Where(root => root != targetAvatar)
                .SelectMany(root => context.GetComponentsInChildren<Renderer>(root, true))
                .ToImmutableHashSet();
        }
    }
}