using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.Admin;

public sealed class AdminPlayer : ModPlayer
{
    public bool IsAdmin;
}

public sealed class GrantAdmin : ModCommand
{
    public override string Command => "omadmin";

    public override CommandType Type => CommandType.Console;

    public override string Description => "Grants or revokes admin status to a player. Usage: /admin <playerName|whoAmI> [revoke]";

    public override string Usage => "/omadmin <playerName|whoAmI> [revoke]";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.netMode != NetmodeID.Server)
        {
            caller.Reply("This command can only be run from the server console.");
            return;
        }

        if (args.Length < 1)
        {
            caller.Reply($"Usage: {Usage}");
            return;
        }

        bool revoke = args.Length >= 2 && args[1].Equals("revoke", System.StringComparison.OrdinalIgnoreCase);

        Player? target = FindPlayer(args[0]);
        if (target is null)
        {
            caller.Reply($"Player \"{args[0]}\" not found or is not connected.");
            return;
        }

        var adminPlayer = target.GetModPlayer<AdminPlayer>();
        adminPlayer.IsAdmin = !revoke;

        AdminConfig.SetAdminName(target.name, !revoke);

        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.AdminStatus);
        packet.Write((byte)target.whoAmI);
        packet.Write(adminPlayer.IsAdmin);
        packet.Send();

        string action = revoke ? "revoked from" : "granted to";
        caller.Reply($"Admin {action} {target.name}.");
    }

    private static Player? FindPlayer(string nameOrId)
    {
        if (int.TryParse(nameOrId, out int id))
        {
            if (id >= 0 && id < Main.maxPlayers)
            {
                var p = Main.player[id];
                return p.active ? p : null;
            }
            return null;
        }

        Player? exact = null;
        Player? partial = null;
        foreach (var p in Main.player)
        {
            if (!p.active) continue;
            if (p.name.Equals(nameOrId, System.StringComparison.OrdinalIgnoreCase))
            {
                exact = p;
                break;
            }
            if (p.name.Contains(nameOrId, System.StringComparison.OrdinalIgnoreCase))
                partial ??= p;
        }

        return exact ?? partial;
    }
}

public sealed class ListAdmins : ModCommand
{
    public override string Command => "omadminlist";
    public override CommandType Type => CommandType.Console;
    public override string Description => "Lists all persistent admins.";
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        var admins = AdminConfig.AdminNames;
        if (admins.Count == 0)
        {
            caller.Reply("No persistent admins registered.");
            return;
        }

        caller.Reply("Persistent Admins:");
        foreach (var name in admins)
            caller.Reply($"- {name}");
    }
}
