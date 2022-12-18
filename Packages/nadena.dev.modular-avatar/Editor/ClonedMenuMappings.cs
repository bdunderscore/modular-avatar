using System.Collections.Generic;
using System.Collections.Immutable;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace nadena.dev.modular_avatar.core.editor 
{
	internal static class ClonedMenuMappings 
	{
		private static readonly Dictionary<VRCExpressionsMenu, ImmutableList<VRCExpressionsMenu>> ClonedMappings =
			new Dictionary<VRCExpressionsMenu, ImmutableList<VRCExpressionsMenu>>();

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