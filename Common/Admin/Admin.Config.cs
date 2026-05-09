using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace OxidizedMediumcore.Common.Admin;

public static class AdminConfig
{
    public const bool ForcePvP = true;
    public static bool DisableGemCraftRecipe { get; set; } = false;

    private static readonly HashSet<string> _adminNames = new(System.StringComparer.OrdinalIgnoreCase);

    public static bool IsAdminName(string name) => _adminNames.Contains(name);

    public static void SetAdminName(string name, bool value)
    {
        if (value) _adminNames.Add(name);
        else _adminNames.Remove(name);
    }

    public static IReadOnlyCollection<string> AdminNames => _adminNames;

    private static float _blastRadiusMultiplier = 0.45f;
    private static float _pickSpeedPenalty = 0.93f;

    public static float BlastRadiusMultiplier
    {
        get => _blastRadiusMultiplier;
        set => _blastRadiusMultiplier = value;
    }
    public static float PickSpeedPenalty
    {
        get => _pickSpeedPenalty;
        set => _pickSpeedPenalty = value;
    }

    public static ref float BlastRadiusMultiplierRef => ref _blastRadiusMultiplier;
    public static ref float PickSpeedPenaltyRef => ref _pickSpeedPenalty;

    public static void BroadcastSync()
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.AdminConfigSync);
        packet.Write(DisableGemCraftRecipe);
        packet.Write(BlastRadiusMultiplier);
        packet.Write(PickSpeedPenalty);
        packet.Send();
    }

    public static void SendChange()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.AdminConfigChange);
        packet.Write(DisableGemCraftRecipe);
        packet.Write(BlastRadiusMultiplier);
        packet.Write(PickSpeedPenalty);
        packet.Send();
    }

    public static void HandleSync(BinaryReader reader)
    {
        DisableGemCraftRecipe = reader.ReadBoolean();
        BlastRadiusMultiplier = reader.ReadSingle();
        PickSpeedPenalty = reader.ReadSingle();
    }

    public static void HandleChange(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        if (!Main.player[whoAmI].GetModPlayer<AdminPlayer>().IsAdmin)
            return;

        DisableGemCraftRecipe = reader.ReadBoolean();
        BlastRadiusMultiplier = reader.ReadSingle();
        PickSpeedPenalty = reader.ReadSingle();
        BroadcastSync();
    }
}

public sealed class AdminSyncOnJoin : ModPlayer
{
    public override void OnEnterWorld()
    {
        var adminPlayer = Player.GetModPlayer<AdminPlayer>();
        adminPlayer.IsAdmin = AdminConfig.IsAdminName(Player.name);

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            var mod = ModContent.GetInstance<OxidizedMediumcore>();

            var statusPacket = mod.GetPacket();
            statusPacket.Write((byte)OxidizedMediumcore.PacketID.AdminStatus);
            statusPacket.Write((byte)Player.whoAmI);
            statusPacket.Write(adminPlayer.IsAdmin);
            statusPacket.Send();
        }
    }
}

public sealed class AdminConfigWorld : ModSystem
{
    public override void SaveWorldData(TagCompound tag)
    {
        tag["disableGemCraft"] = AdminConfig.DisableGemCraftRecipe;
        tag["blastRadiusMult"] = AdminConfig.BlastRadiusMultiplier;
        tag["pickSpeedPenalty"] = AdminConfig.PickSpeedPenalty;
        tag["adminNames"] = new List<string>(AdminConfig.AdminNames);
    }

    public override void LoadWorldData(TagCompound tag)
    {
        AdminConfig.DisableGemCraftRecipe = tag.ContainsKey("disableGemCraft") && tag.Get<bool>("disableGemCraft");

        AdminConfig.BlastRadiusMultiplier = tag.ContainsKey("blastRadiusMult") ? tag.Get<float>("blastRadiusMult") : 0.45f;

        AdminConfig.PickSpeedPenalty = tag.ContainsKey("pickSpeedPenalty") ? tag.Get<float>("pickSpeedPenalty") : 0.93f;

        if (tag.ContainsKey("adminNames"))
        {
            foreach (string name in tag.GetList<string>("adminNames"))
                AdminConfig.SetAdminName(name, true);
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(AdminConfig.DisableGemCraftRecipe);
        writer.Write(AdminConfig.BlastRadiusMultiplier);
        writer.Write(AdminConfig.PickSpeedPenalty);

        var admins = AdminConfig.AdminNames;
        writer.Write(admins.Count);
        foreach (var name in admins)
        {
            writer.Write(name);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        AdminConfig.DisableGemCraftRecipe = reader.ReadBoolean();
        AdminConfig.BlastRadiusMultiplier = reader.ReadSingle();
        AdminConfig.PickSpeedPenalty = reader.ReadSingle();

        foreach (string name in new List<string>(AdminConfig.AdminNames))
            AdminConfig.SetAdminName(name, false);

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            AdminConfig.SetAdminName(reader.ReadString(), true);
        }
    }

    public override void OnWorldUnload()
    {
        AdminConfig.DisableGemCraftRecipe = false;
        AdminConfig.BlastRadiusMultiplier = 0.25f;
        AdminConfig.PickSpeedPenalty = 0.93f;
        foreach (string name in new List<string>(AdminConfig.AdminNames))
            AdminConfig.SetAdminName(name, false);
    }
}