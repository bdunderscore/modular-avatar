#if MA_VRM1

using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core.vrm;

using UniVRM10;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    internal class MergeVRM1SpringBonesPass : Pass<MergeVRM1SpringBonesPass>
    {
        protected override void Execute(ndmf.BuildContext context)
        {
            var processor = new MergeVRM1SpringBoneProcessor();
            processor.ProcessVRM1(context);
        }
    }

    internal class MergeVRM1SpringBoneProcessor
    {
        public void ProcessVRM1(ndmf.BuildContext context)
        {
            var rootTransform = context.AvatarRootObject;
            var vrmInstance = rootTransform.GetComponent<Vrm10Instance>();
            if (!vrmInstance) return;

            var sources = rootTransform.GetComponentsInChildren<ModularAvatarMergeVRM1SpringBones>(); 

            vrmInstance.SpringBone.ColliderGroups = vrmInstance.SpringBone.ColliderGroups
                .Concat(sources.SelectMany(bone => bone.colliderGroups))
                .ToList();
            
            vrmInstance.SpringBone.Springs = vrmInstance.SpringBone.Springs
                .Concat(sources.SelectMany(bone => bone.springs))
                .ToList();

            foreach (var source in sources)
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}

#endif
