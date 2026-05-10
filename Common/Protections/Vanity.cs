using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.Protections;

public sealed class VanityProtection : ModSystem
{
    public override void Load() => IL_Player.DropItems += PatchDropItems;
    public override void Unload() => IL_Player.DropItems -= PatchDropItems;

    public static void TryDropWrapper(Player player, IEntitySource source, Item item)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            player.TryDroppingSingleItem(source, item);
            return;
        }

        if (item is { IsAir: false } && (item.vanity || item.dye > 0))
            return;

        player.TryDroppingSingleItem(source, item);
    }

    private static void PatchDropItems(ILContext il)
    {
        var c = new ILCursor(il);

        while (c.TryGotoNext(MoveType.Before, i => i.MatchCall<Player>("TryDroppingSingleItem") || i.MatchCallvirt<Player>("TryDroppingSingleItem")))
        {
            c.Next!.OpCode = OpCodes.Call;
            c.Next!.Operand = typeof(VanityProtection).GetMethod(nameof(TryDropWrapper))!;
            c.Index++;
        }
    }
}