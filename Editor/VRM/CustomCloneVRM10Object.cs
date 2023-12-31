#if MA_VRM1

using UnityEditor;
using UnityEngine;
using UniVRM10;

namespace nadena.dev.modular_avatar.core.editor.vrm
{
    internal class CustomCloneVRM10Object : CustomClone<VRM10Object>
    {
        readonly bool _rename;
        readonly bool _copyThumbnail;

        public CustomCloneVRM10Object(bool rename = false, bool copyThumbnail = false)
        {
            _rename = rename;
            _copyThumbnail = copyThumbnail;
        }
        
        protected override void Define(CopyStrategyDescriptor descriptor)
        {
            descriptor.DeepCopy<VRM10Object>(postProcess: Rename);
            descriptor.DeepCopy<VRM10Expression>(postProcess: Rename);

            if (_copyThumbnail)
            {
                descriptor.DeepCopy<Texture2D>();
            }
            else
            {
                descriptor.ShallowCopy<Texture2D>();
            }
            
            descriptor.ShallowCopy<MonoScript>();
            descriptor.ShallowCopy<GameObject>();

            void Rename(Object obj)
            {
                if (_rename) obj.name = $"{obj.name} (Clone)";
            }
        }
    }
}

#endif