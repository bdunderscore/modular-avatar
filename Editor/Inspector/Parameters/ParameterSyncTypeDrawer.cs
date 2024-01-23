#if MA_VRCSDK3_AVATARS
using System;
using UnityEditor;

namespace nadena.dev.modular_avatar.core.editor
{
    [CustomPropertyDrawer(typeof(ParameterSyncType))]
    internal class ParameterSyncTypeDrawer : EnumDrawer<ParameterSyncType>
    {
        protected override string localizationPrefix => "params.syncmode";

        protected override Array enumValues => new object[]
        {
            ParameterSyncType.NotSynced,
            ParameterSyncType.Bool,
            ParameterSyncType.Float,
            ParameterSyncType.Int,
        };
    }
}
#endif