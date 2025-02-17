#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.modular_avatar.animation
{
    [DependsOnContext(typeof(AnimatorServicesContext))]
    internal class ReadablePropertyExtension : IExtensionContext
    {
        // This is a temporary hack for GameObjectDelayDisablePass
        public class Retained
        {
            public Dictionary<EditorCurveBinding, string> proxyProps = new();
        }

        private AnimatorServicesContext? _asc;
        private Retained _retained = null!;

        private AnimatorServicesContext asc =>
            _asc ?? throw new InvalidOperationException("ActiveSelfProxyExtension is not active");

        private Dictionary<EditorCurveBinding, string> proxyProps => _retained.proxyProps;
        private int index;

        public IEnumerable<(EditorCurveBinding, string)> ActiveProxyProps =>
            proxyProps.Select(kvp => (kvp.Key, kvp.Value));

        public string GetActiveSelfProxy(GameObject obj)
        {
            var path = asc.ObjectPathRemapper.GetVirtualPathForObject(obj);
            var ecb = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");

            if (proxyProps.TryGetValue(ecb, out var prop)) return prop;

            prop = $"__MA/ActiveSelfProxy/{obj.name}##{index++}";
            proxyProps[ecb] = prop;

            // Add prop to all animators
            foreach (var animator in asc.ControllerContext.GetAllControllers())
            {
                animator.Parameters = animator.Parameters.SetItem(
                    prop,
                    new AnimatorControllerParameter
                    {
                        name = prop,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = obj.activeSelf ? 1 : 0
                    }
                );
            }

            return prop;
        }

        public void OnActivate(BuildContext context)
        {
            _asc = context.Extension<AnimatorServicesContext>();
            _retained = context.GetState<Retained>();
        }

        public void OnDeactivate(BuildContext context)
        {
            asc.AnimationIndex.EditClipsByBinding(proxyProps.Keys, clip =>
            {
                foreach (var b in clip.GetFloatCurveBindings().ToList())
                {
                    if (proxyProps.TryGetValue(b, out var proxyProp))
                    {
                        var curve = clip.GetFloatCurve(b);
                        clip.SetFloatCurve("", typeof(Animator), proxyProp, curve);
                    }
                }
            });
        }
    }
}