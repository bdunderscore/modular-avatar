#if MA_VRM0

using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core.vrm;
using VRM;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    internal class MergeVRM0FirstPersonPass : Pass<MergeVRM0FirstPersonPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var processor = new MergeVrm0FirstPersonProcessor();
            processor.ProcessVRM0(context);
        }
    }

    internal class MergeVrm0FirstPersonProcessor
    {
        public void ProcessVRM0(ndmf.BuildContext context)
        {
            var rootTransform = context.AvatarRootObject;
            var vrmFirstPerson = rootTransform.GetComponent<VRMFirstPerson>();
            if (!vrmFirstPerson) return;

            var sources = rootTransform.GetComponentsInChildren<ModularAvatarMergeVRMFirstPerson>(); 

            vrmFirstPerson.Renderers.AddRange(sources.SelectMany(source => source.renderers)
                .Select(renderer => new VRMFirstPerson.RendererFirstPersonFlags
                {
                    Renderer = renderer.renderer,
                    FirstPersonFlag = renderer.VRM0FirstPersonFlag
                }));

            foreach (var source in sources)
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}

#endif
