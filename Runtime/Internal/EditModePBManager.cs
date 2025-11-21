using UnityEngine;

namespace nadena.dev.modular_avatar.editor.fit_preview
{
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    public class EditModePBManager 
        #if MA_VRCSDK3_AVATARS
            : VRC.Dynamics.PhysBoneManager
        #endif
    {
    }
}