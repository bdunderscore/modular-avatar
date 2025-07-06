using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    internal interface IModularAvatarMaterialChanger
    {
    }

    internal interface IObjSwap<TObj> where TObj : Object
    {
        TObj From { get; set; }
        TObj To { get; set; }
    }

    internal interface IObjectSwap<TObj, TObjSwap> where TObj : Object where TObjSwap : IObjSwap<TObj>
    {
        List<TObjSwap> Swaps { get; }
    }
}
