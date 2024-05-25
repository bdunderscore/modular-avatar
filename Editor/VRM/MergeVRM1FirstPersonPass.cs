#if MA_VRM1

using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core.vrm;
using UniVRM10;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    internal class MergeVRM1FirstPersonPass : Pass<MergeVRM1FirstPersonPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var processor = new MergeVrm1FirstPersonProcessor();
            processor.ProcessVRM1(context);
        }
    }

    internal class MergeVrm1FirstPersonProcessor
    {
        public void ProcessVRM1(ndmf.BuildContext context)
        {
            var rootTransform = context.AvatarRootObject;
            var vrmInstance = rootTransform.GetComponent<Vrm10Instance>();
            if (!vrmInstance) return;

            var sources = rootTransform.GetComponentsInChildren<ModularAvatarMergeVRMFirstPerson>();

            vrmInstance.Vrm = new CustomCloneVRM10Object().Clone(vrmInstance.Vrm).mainAsset;
            vrmInstance.Vrm.FirstPerson.Renderers.AddRange(sources.SelectMany(source => source.renderers)
                .Select(renderer => new RendererFirstPersonFlags
                {
                    Renderer = RuntimeUtil.RelativePath(context.AvatarRootObject, renderer.renderer.gameObject),
                    FirstPersonFlag = renderer.VRM1FirstPersonType
                }));

            foreach (var source in sources)
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}

#endif
