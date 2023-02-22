using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace nadena.dev.modular_avatar.core
{
    /// <summary>
    /// The menu install target includes the controls of the target menu installer at the point of reference.
    /// Notably, this can include multiple controls.
    ///
    /// One tricky aspect of this feature is that we need to disambiguate when a menu installer also cites a target menu.
    ///  Generally, if an installer is targeted by any menu install target (even if - especially if - disabled), we
    /// ignore its install target configuration entirely.
    ///
    /// We can also end up with a loop between install targets; in this case, we break the loop at an arbitrary point.
    /// </summary>
    internal class ModularAvatarMenuInstallTarget : MenuSource
    {
        public ModularAvatarMenuInstaller installer;

        private static HashSet<MenuSource> _recursing = new HashSet<MenuSource>();

        internal delegate T Returning<T>();

        /**
         * Temporarily clears the list of install targets we're recursing through. This is useful if we need to generate
         * a submenu; these have their own recursion stack, and we shouldn't truncate the set of controls registered on
         * a different submenu that happens to transclude the same point.
         */
        internal static T PushRecursing<T>(Returning<T> callback)
        {
            HashSet<MenuSource> oldRecursing = _recursing;
            _recursing = new HashSet<MenuSource>();
            try
            {
                return callback();
            }
            finally
            {
                _recursing = oldRecursing;
            }
        }

        internal override VRCExpressionsMenu.Control[] GenerateMenu()
        {
            if (installer == null) return new VRCExpressionsMenu.Control[] { };

            _recursing.Add(this);
            try
            {
                var source = installer.GetComponent<MenuSource>();
                if (source != null)
                {
                    return source.GenerateMenu();
                }
                else
                {
                    // ReSharper disable once Unity.NoNullPropagation
                    return installer.menuToAppend?.controls?.ToArray() ?? new VRCExpressionsMenu.Control[] { };
                }
            }
            finally
            {
                _recursing.Remove(this);
            }
        }
    }
}