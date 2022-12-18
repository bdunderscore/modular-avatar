using System.Collections.Generic;
using System.Collections.Immutable;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor 
{
	internal static class ClonedMenuMappings 
	{
		/// <summary>
		/// Map to link the cloned menu from the clone source.
		/// If one menu is specified for multiple installers, they are replicated separately, so there is a one-to-many relationship.
		/// </summary>
		private static readonly Dictionary<VRCExpressionsMenu, ImmutableList<VRCExpressionsMenu>> ClonedMappings =
			new Dictionary<VRCExpressionsMenu, ImmutableList<VRCExpressionsMenu>>();

		/// <summary>
		/// Map to link the clone source from the cloned menu.
		/// Map is the opposite of ClonedMappings.
		/// </summary>
		private static readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> OriginalMapping =
			new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

		public static void Clear() 
		{
			ClonedMappings.Clear();
			OriginalMapping.Clear();
		}

		public static void Add(VRCExpressionsMenu original, VRCExpressionsMenu clonedMenu) 
		{
			if (!ClonedMappings.TryGetValue(original, out ImmutableList<VRCExpressionsMenu> clonedMenus)) 
			{
				clonedMenus = ImmutableList<VRCExpressionsMenu>.Empty;
			}
			ClonedMappings[original] = clonedMenus.Add(clonedMenu);
			OriginalMapping[clonedMenu] = original;
		}

		public static bool TryGetClonedMenus(VRCExpressionsMenu original, out ImmutableList<VRCExpressionsMenu> clonedMenus) 
		{
			return ClonedMappings.TryGetValue(original, out clonedMenus);
		}
		
		public static VRCExpressionsMenu GetOriginal(VRCExpressionsMenu cloned) 
		{
			return OriginalMapping.TryGetValue(cloned, out VRCExpressionsMenu original) ? original : null;
		}
	}
}