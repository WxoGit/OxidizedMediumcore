using System.Linq;
using OxidizedMediumcore.Common.Admin;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.Protections;

public sealed class BlockGiantGemsCraft : ModSystem
{
	private static readonly int[] GiantGemIDs = new int[7] { 1522, 1523, 1524, 1525, 1526, 1527, 3643 };

	public override void PostAddRecipes()
	{
		if (Main.netMode == NetmodeID.SinglePlayer)
		    return;

		Recipe[] recipe = Main.recipe;
		foreach (Recipe recipe2 in recipe)
		{
			if (GiantGemIDs.Contains(recipe2.createItem.type))
			{
				recipe2.AddCondition(LocalizedText.Empty, () => AdminConfig.DisableGemCraftRecipe);
			}
		}
	}
}