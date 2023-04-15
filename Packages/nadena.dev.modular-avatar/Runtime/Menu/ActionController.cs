using UnityEngine;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// Tag class that marks components that actions can be attached to.
    ///
    /// Note that this is public due to C# protection rules, but is not a supported API for editor scripts and may
    /// change in point releases.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ActionController : AvatarTagComponent
    {
        internal abstract bool isSyncedProp { get; }
        internal abstract bool isSavedProp { get; }
    }
}