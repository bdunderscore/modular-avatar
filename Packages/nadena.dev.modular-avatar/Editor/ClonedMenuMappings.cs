using System.Collections.Generic;
using System.Collections.Immutable;
using VRC.SDK3.Avatars.ScriptableObjects;

// ReSharper disable once CheckNamespace
namespace nadena.dev.modular_avatar.core.editor 
{
	public static class ClonedMenuMappings {
		private static readonly Dictionary<VRCExpressionsMenu, ImmutableArray<VRCExpressionsMenu>> ClonedMappings =
			new Dictionary<VRCExpressionsMenu, ImmutableArray<VRCExpressionsMenu>>();

		private static readonly Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> OriginalMapping =
			new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();

		public static void Clear() 
		{
			ClonedMappings.Clear();
			OriginalMapping.Clear();
		}

		public static void Add(VRCExpressionsMenu original, VRCExpressionsMenu clonedMenu) 
		{
			if (!ClonedMappings.TryGetValue(original, out ImmutableArray<VRCExpressionsMenu> clonedMenus)) 
			{
				clonedMenus = ImmutableArray<VRCExpressionsMenu>.Empty;
			}
			// Usually, one menu is rarely duplicated in multiple menus, so don't bother using a Builder
			ClonedMappings[original] = clonedMenus.Add(clonedMenu);
			OriginalMapping[clonedMenu] = original;
		}

		public static bool TryGetClonedMenus(VRCExpressionsMenu original, out ImmutableArray<VRCExpressionsMenu> clonedMenus) 
		{
			return ClonedMappings.TryGetValue(original, out clonedMenus);
		}
		
		public static VRCExpressionsMenu GetOriginal(VRCExpressionsMenu cloned) 
		{
			return OriginalMapping.TryGetValue(cloned, out VRCExpressionsMenu original) ? original : null;
		}
	}
}