using OxidizedMediumcore.Common.Admin;
using OxidizedMediumcore.Common.BossOwnership;
using OxidizedMediumcore.Common.Gemlocks;
using OxidizedMediumcore.Common.Glory;
using OxidizedMediumcore.Common.TeamControl;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore;

public class OxidizedMediumcore : Mod
{
    public enum PacketID : byte { AdminStatus, ZoneSync, ZoneSyncFull, AdminConfigSync, AdminConfigChange, HasSelectedTeamSync, BossOwnerSync, ZoneSetRequest, ZoneToggleProtectionRequest, ZoneToggleRequest, RemoveGemRequest, GlorySync }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        switch ((PacketID)reader.ReadByte())
        {
            case PacketID.AdminStatus:
                HandleAdminStatus(reader, whoAmI);
                break;
            case PacketID.ZoneSync:
                GemLockZones.HandleZoneSync(reader, whoAmI);
                break;
            case PacketID.ZoneSyncFull:
                GemLockZones.HandleZoneSyncFull(reader);
                break;
            case PacketID.AdminConfigSync:
                AdminConfig.HandleSync(reader);
                break;
            case PacketID.AdminConfigChange:
                AdminConfig.HandleChange(reader, whoAmI);
                break;
            case PacketID.HasSelectedTeamSync:
                HasSelectedTeam.HandleSync(reader, whoAmI);
                break;
            case PacketID.BossOwnerSync:
                BossOwnershipSync.HandleSync(reader);
                break;
            case PacketID.ZoneSetRequest:
                GemLockZones.HandleSetZoneRequest(reader, whoAmI);
                break;
            case PacketID.ZoneToggleProtectionRequest:
                GemLockZones.HandleToggleProtectionRequest(reader, whoAmI);
                break;
            case PacketID.ZoneToggleRequest:
                GemLockZones.HandleToggleZoneRequest(reader, whoAmI);
                break;
            case PacketID.RemoveGemRequest:
                HandleRemoveGemRequest(reader, whoAmI);
                break;
            case PacketID.GlorySync:
                TeamGlorySystem.HandleGlorySync(reader, whoAmI);
                break;
        }
    }

    private void HandleAdminStatus(BinaryReader reader, int whoAmI)
    {
        byte playerIndex = reader.ReadByte();
        bool isAdmin = reader.ReadBoolean();
        Main.player[playerIndex].GetModPlayer<AdminPlayer>().IsAdmin = isAdmin;

        if (Main.netMode != NetmodeID.Server)
            return;

        var packet = GetPacket();
        packet.Write((byte)PacketID.AdminStatus);
        packet.Write(playerIndex);
        packet.Write(isAdmin);
        packet.Send(ignoreClient: whoAmI);
    }

    private static void HandleRemoveGemRequest(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        var origin = new Microsoft.Xna.Framework.Point(reader.ReadInt32(), reader.ReadInt32());

        if (!GemLockHelper.HasGem(origin))
            return;

        GemLockRadialSystem.DoRemoveGem(origin);
    }
}