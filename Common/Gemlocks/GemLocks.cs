using Microsoft.Xna.Framework;
using OxidizedMediumcore.Common.Gemlocks;
using OxidizedMediumcore.Common.Glory;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace OxidizedMediumcore.Common.Gemlocks;

public sealed class GemLockZones : ModSystem
{
    public const int DefaultHalfW = TeamGlorySystem.BaseHalfW;
    public const int DefaultHalfH = TeamGlorySystem.BaseHalfH;

    public const int DefaultHalf = DefaultHalfW;

    public const int MinHalf = 5;

    public const int MaxHalf = TeamGlorySystem.HardCapHalfW;

    private const float TileSize = 16f;

    private readonly Dictionary<Point, GemLockZone> _zones = [];

    private readonly HashSet<Point> _hasGem = [];

    private readonly HashSet<Point> _zoneActive = [];

    public IEnumerable<Point> ActiveZones => _zoneActive;

    public GemLockZone GetZone(Point origin) => _zones.TryGetValue(origin, out var z) ? z : new GemLockZone(DefaultHalfW, DefaultHalfH);

    public int GetActivationTimer(Point origin) => _zones.TryGetValue(origin, out var z) ? z.ActivationTimer : 0;


    private static int GetTeamMaxHalfW(Point origin)
    {
        Team team = GemLockHelper.GetTeamForOrigin(origin);
        if (team == Team.None)
            return TeamGlorySystem.HardCapHalfW;

        return ModContent.GetInstance<TeamGlorySystem>().GetMaxHalfWidth(team);
    }

    private static int GetTeamMaxHalfH(Point origin)
    {
        Team team = GemLockHelper.GetTeamForOrigin(origin);
        if (team == Team.None)
            return TeamGlorySystem.HardCapHalfH;

        return ModContent.GetInstance<TeamGlorySystem>().GetMaxHalfHeight(team);
    }


    private void ApplySetZone(Point origin, int halfWidth, int halfHeight)
    {
        var z = _zones.TryGetValue(origin, out var existing) ? existing : new GemLockZone(DefaultHalfW, DefaultHalfH);

        int newHW = Utils.Clamp(halfWidth, MinHalf, GetTeamMaxHalfW(origin));
        int newHH = Utils.Clamp(halfHeight, MinHalf, GetTeamMaxHalfH(origin));

        if (z.HalfWidth != newHW || z.HalfHeight != newHH)
            z.ActivationTimer = 0;

        z.HalfWidth = newHW;
        z.HalfHeight = newHH;

        _zones[origin] = z;
    }

    public void EnforceLimitsForTeam(Team team)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        foreach (var origin in new List<Point>(_zones.Keys))
        {
            if (GemLockHelper.GetTeamForOrigin(origin) != team)
                continue;

            var z = _zones[origin];
            int maxW = GetTeamMaxHalfW(origin);
            int maxH = GetTeamMaxHalfH(origin);

            bool changed = false;
            if (z.HalfWidth > maxW) { z.HalfWidth = maxW; changed = true; }
            if (z.HalfHeight > maxH) { z.HalfHeight = maxH; changed = true; }

            if (changed)
            {
                z.ActivationTimer = 0;
                _zones[origin] = z;
                if (Main.netMode == NetmodeID.Server)
                    SendZoneSync(origin, toClient: -1, ignoreClient: -1);
            }
        }
    }

    public void SetZone(Point origin, int halfWidth, int halfHeight)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            SendSetZoneRequest(origin, halfWidth, halfHeight);
            ApplySetZone(origin, halfWidth, halfHeight);
            return;
        }

        ApplySetZone(origin, halfWidth, halfHeight);
        SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    private void ApplyToggleZone(Point origin)
    {
        if (!_hasGem.Contains(origin))
            return;

        var z = _zones.TryGetValue(origin, out var existing) ? existing : new GemLockZone(DefaultHalfW, DefaultHalfH);

        if (!z.ZoneEnabled)
        {
            Team team = GemLockHelper.GetTeamForOrigin(origin);
            if (team != Team.None)
            {
                foreach (var other in _zoneActive)
                {
                    if (other != origin && GemLockHelper.GetTeamForOrigin(other) == team)
                        return;
                }
            }

            z.ZoneEnabled = true;
            z.ActivationTimer = 0;
            _zones[origin] = z;
            _zoneActive.Add(origin);
        }
        else
        {
            z.ZoneEnabled = false;
            z.ActivationTimer = 0;
            _zones[origin] = z;
            _zoneActive.Remove(origin);
        }
    }

    private void ApplyToggleProtection(Point origin)
    {
        var z = _zones.TryGetValue(origin, out var existing) ? existing : new GemLockZone(DefaultHalfW, DefaultHalfH);

        z.ProtectionEnabled = !z.ProtectionEnabled;
        z.ActivationTimer = 0;
        _zones[origin] = z;
    }

    public void ToggleZone(Point origin)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            SendToggleZoneRequest(origin);
            ApplyToggleZone(origin);
            return;
        }

        ApplyToggleZone(origin);
        SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    public void ToggleProtection(Point origin)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            SendToggleProtectionRequest(origin);
            ApplyToggleProtection(origin);
            return;
        }

        ApplyToggleProtection(origin);
        SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    public bool IsZoneEnabled(Point origin) => _zones.TryGetValue(origin, out var z) && z.ZoneEnabled;

    public bool IsProtectionEnabled(Point origin) => _zones.TryGetValue(origin, out var z) && z.ZoneEnabled && z.ProtectionEnabled;

    public Rectangle GetZoneRect(Point origin)
    {
        var z = GetZone(origin);
        int cx = origin.X + 1;
        int cy = origin.Y + 1;
        return new Rectangle((cx - z.HalfWidth) * 16, (cy - z.HalfHeight) * 16, (z.HalfWidth * 2 + 1) * 16, (z.HalfHeight * 2 + 1) * 16);
    }

    public bool IsInsideZone(Point origin, int tx, int ty)
    {
        var z = GetZone(origin);
        int cx = origin.X + 1;
        int cy = origin.Y + 1;
        return tx >= cx - z.HalfWidth && tx <= cx + z.HalfWidth
            && ty >= cy - z.HalfHeight && ty <= cy + z.HalfHeight;
    }

    public bool IsInsideEnemyZone(int tx, int ty, Team ownerTeam)
    {
        foreach (var origin in _zoneActive)
        {
            var z = GetZone(origin);
            if (!z.ProtectionEnabled)
                continue;
            if (!IsInsideZone(origin, tx, ty))
                continue;
            var required = GemLockHelper.GetTeamForOrigin(origin);
            if (required == Team.None || ownerTeam == required)
                continue;
            return true;
        }
        return false;
    }

    public void RefreshLock(Point origin)
    {
        bool hadGem = _hasGem.Contains(origin);
        bool hasGem = GemLockHelper.HasGem(origin);

        if (!hasGem)
        {
            bool wasZoneActive = _zoneActive.Contains(origin);
            _hasGem.Remove(origin);
            _zoneActive.Remove(origin);
            if (_zones.TryGetValue(origin, out var zClean))
            {
                zClean.ZoneEnabled = false;
                zClean.ActivationTimer = 0;
                _zones[origin] = zClean;
            }

            if ((hadGem || wasZoneActive) && Main.netMode == NetmodeID.Server)
                SendZoneSync(origin, toClient: -1, ignoreClient: -1);
            return;
        }

        Team team = GemLockHelper.GetTeamForOrigin(origin);

        if (team == Team.None)
        {
            bool wasZoneActive = _zoneActive.Contains(origin);
            _hasGem.Remove(origin);
            _zoneActive.Remove(origin);
            if ((hadGem || wasZoneActive) && Main.netMode == NetmodeID.Server)
                SendZoneSync(origin, toClient: -1, ignoreClient: -1);
            return;
        }

        if (!hadGem)
        {
            var z = _zones.TryGetValue(origin, out var existing) ? existing : new GemLockZone(DefaultHalfW, DefaultHalfH);
            z.ZoneEnabled = false;
            z.ActivationTimer = 0;
            _zones[origin] = z;
        }
        _hasGem.Add(origin);

        if (Main.netMode == NetmodeID.Server)
            SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    public bool HasGem(Point origin) => _hasGem.Contains(origin);

    public bool HasOverlappingEnemyZone(Point origin)
    {
        Team team = GemLockHelper.GetTeamForOrigin(origin);
        Rectangle candidate = GetZoneRect(origin);

        foreach (var other in _zoneActive)
        {
            if (GemLockHelper.GetTeamForOrigin(other) == team)
                continue;

            if (!IsProtectionEnabled(other))
                continue;

            if (candidate.Intersects(GetZoneRect(other)))
                return true;
        }
        return false;
    }

    public bool HasActiveZoneElsewhere(Point origin)
    {
        Team team = GemLockHelper.GetTeamForOrigin(origin);
        if (team == Team.None)
            return false;

        foreach (var other in _zoneActive)
        {
            if (other != origin && GemLockHelper.GetTeamForOrigin(other) == team)
                return true;
        }
        return false;
    }

    public void SendFullSyncTo(int toClient)
    {
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.ZoneSyncFull);
        packet.Write(_zones.Count);
        foreach (var (origin, zone) in _zones)
        {
            packet.Write(origin.X);
            packet.Write(origin.Y);
            packet.Write(zone.HalfWidth);
            packet.Write(zone.HalfHeight);
            packet.Write(_hasGem.Contains(origin));
            packet.Write(_zoneActive.Contains(origin));
            packet.Write(zone.ProtectionEnabled);
            packet.Write(zone.ZoneEnabled);
        }
        packet.Send(toClient);
    }

    private void SendZoneSync(Point origin, int toClient, int ignoreClient)
    {
        var zone = GetZone(origin);
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.ZoneSync);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Write(zone.HalfWidth);
        packet.Write(zone.HalfHeight);
        packet.Write(_hasGem.Contains(origin));
        packet.Write(_zoneActive.Contains(origin));
        packet.Write(zone.ProtectionEnabled);
        packet.Write(zone.ZoneEnabled);
        packet.Send(toClient, ignoreClient);
    }

    private static void SendSetZoneRequest(Point origin, int halfWidth, int halfHeight)
    {
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.ZoneSetRequest);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Write(halfWidth);
        packet.Write(halfHeight);
        packet.Send();
    }

    private static void SendToggleZoneRequest(Point origin)
    {
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.ZoneToggleRequest);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Send();
    }

    private static void SendToggleProtectionRequest(Point origin)
    {
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.ZoneToggleProtectionRequest);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Send();
    }

    public static void HandleZoneSync(BinaryReader reader, int whoAmI)
    {
        var origin = new Point(reader.ReadInt32(), reader.ReadInt32());
        int hw = reader.ReadInt32();
        int hh = reader.ReadInt32();
        bool hasGem = reader.ReadBoolean();
        bool zoneActive = reader.ReadBoolean();
        bool protectionEnabled = reader.ReadBoolean();
        bool zoneEnabled = reader.ReadBoolean();

        var inst = ModContent.GetInstance<GemLockZones>();
        var z = inst._zones.TryGetValue(origin, out var existing) ? existing : new GemLockZone(DefaultHalfW, DefaultHalfH);

        if (z.HalfWidth != hw || z.HalfHeight != hh)
            z.ActivationTimer = 0;

        z.HalfWidth = hw;
        z.HalfHeight = hh;
        z.ProtectionEnabled = protectionEnabled;
        z.ZoneEnabled = zoneEnabled;
        inst._zones[origin] = z;

        if (hasGem)
            inst._hasGem.Add(origin);
        else
            inst._hasGem.Remove(origin);

        if (zoneActive)
            inst._zoneActive.Add(origin);
        else
            inst._zoneActive.Remove(origin);

        if (Main.netMode == NetmodeID.Server)
            inst.SendZoneSync(origin, toClient: -1, ignoreClient: whoAmI);
    }

    public static void HandleZoneSyncFull(BinaryReader reader)
    {
        var inst = ModContent.GetInstance<GemLockZones>();
        inst._zones.Clear();
        inst._hasGem.Clear();
        inst._zoneActive.Clear();

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var origin = new Point(reader.ReadInt32(), reader.ReadInt32());
            int hw = reader.ReadInt32();
            int hh = reader.ReadInt32();
            bool hasGem = reader.ReadBoolean();
            bool zoneActive = reader.ReadBoolean();
            bool protEnabled = reader.ReadBoolean();
            bool zoneEnabled = reader.ReadBoolean();

            var newZone = new GemLockZone(hw, hh) { ProtectionEnabled = protEnabled, ZoneEnabled = zoneEnabled };
            inst._zones[origin] = newZone;
            if (hasGem)
                inst._hasGem.Add(origin);
            if (zoneActive)
                inst._zoneActive.Add(origin);
        }
    }

    public static void HandleSetZoneRequest(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        var origin = new Point(reader.ReadInt32(), reader.ReadInt32());
        int hw = reader.ReadInt32();
        int hh = reader.ReadInt32();

        Team lockTeam = GemLockHelper.GetTeamForOrigin(origin);
        if (lockTeam != Team.None && (Team)Main.player[whoAmI].team != lockTeam)
            return;

        var inst = ModContent.GetInstance<GemLockZones>();
        inst.ApplySetZone(origin, hw, hh);
        inst.SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    public static void HandleToggleZoneRequest(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        var origin = new Point(reader.ReadInt32(), reader.ReadInt32());

        var inst = ModContent.GetInstance<GemLockZones>();
        bool zoneCurrentlyEnabled = inst.IsZoneEnabled(origin);
        if (!zoneCurrentlyEnabled)
        {
            Team lockTeam = GemLockHelper.GetTeamForOrigin(origin);
            if (lockTeam != Team.None && (Team)Main.player[whoAmI].team != lockTeam)
                return;
        }

        inst.ApplyToggleZone(origin);
        inst.SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    public static void HandleToggleProtectionRequest(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        var origin = new Point(reader.ReadInt32(), reader.ReadInt32());

        var inst = ModContent.GetInstance<GemLockZones>();

        bool tryingToEnable = inst.IsZoneEnabled(origin) && !inst.IsProtectionEnabled(origin);
        if (tryingToEnable)
        {
            Team lockTeam = GemLockHelper.GetTeamForOrigin(origin);
            if (lockTeam != Team.None && (Team)Main.player[whoAmI].team != lockTeam)
                return;
        }

        inst.ApplyToggleProtection(origin);
        inst.SendZoneSync(origin, toClient: -1, ignoreClient: -1);
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        foreach (var origin in _zoneActive)
        {
            var z = _zones.TryGetValue(origin, out var zone) ? zone : new GemLockZone(DefaultHalfW, DefaultHalfH);
            if (z.ProtectionEnabled)
                z.ActivationTimer++;
            else
                z.ActivationTimer = 0;
            _zones[origin] = z;
        }
    }

    public override void SaveWorldData(TagCompound tag)
    {
        List<TagCompound> list = [];
        foreach (var (origin, zone) in _zones)
        {
            list.Add(new TagCompound { ["x"] = origin.X, ["y"] = origin.Y, ["hw"] = zone.HalfWidth, ["hh"] = zone.HalfHeight, ["prot"] = zone.ProtectionEnabled, ["zoneEnabled"] = zone.ZoneEnabled, ["hasGem"] = _hasGem.Contains(origin), ["zoneActive"] = _zoneActive.Contains(origin) });
        }
        tag["zones"] = list;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        _zones.Clear();
        _hasGem.Clear();
        _zoneActive.Clear();

        if (!tag.ContainsKey("zones"))
            return;

        foreach (var entry in tag.GetList<TagCompound>("zones"))
        {
            var origin = new Point(entry.GetInt("x"), entry.GetInt("y"));
            int hw = entry.GetInt("hw");
            int hh = entry.GetInt("hh");
            bool prot = !entry.ContainsKey("prot") || entry.Get<bool>("prot");
            bool zoneEnabled = entry.ContainsKey("zoneEnabled") && entry.Get<bool>("zoneEnabled");
            bool hasGem = entry.ContainsKey("hasGem") && entry.Get<bool>("hasGem");
            bool zoneActive = entry.ContainsKey("zoneActive")
                ? entry.Get<bool>("zoneActive")
                : (entry.ContainsKey("active") && entry.Get<bool>("active") && zoneEnabled);

            _zones[origin] = new GemLockZone(hw, hh) { ProtectionEnabled = prot, ZoneEnabled = zoneEnabled };
            if (hasGem)
                _hasGem.Add(origin);
            if (zoneActive)
                _zoneActive.Add(origin);
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(_zones.Count);
        foreach (var kvp in _zones)
        {
            writer.Write(kvp.Key.X);
            writer.Write(kvp.Key.Y);
            writer.Write(kvp.Value.HalfWidth);
            writer.Write(kvp.Value.HalfHeight);
            writer.Write(kvp.Value.ProtectionEnabled);
            writer.Write(kvp.Value.ZoneEnabled);
            writer.Write(_hasGem.Contains(kvp.Key));
            writer.Write(_zoneActive.Contains(kvp.Key));
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        _zones.Clear();
        _hasGem.Clear();
        _zoneActive.Clear();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var origin = new Point(reader.ReadInt32(), reader.ReadInt32());
            int hw = reader.ReadInt32();
            int hh = reader.ReadInt32();
            bool prot = reader.ReadBoolean();
            bool zoneEnabled = reader.ReadBoolean();
            bool hasGem = reader.ReadBoolean();
            bool zoneActive = reader.ReadBoolean();

            _zones[origin] = new GemLockZone(hw, hh) { ProtectionEnabled = prot, ZoneEnabled = zoneEnabled };
            if (hasGem) _hasGem.Add(origin);
            if (zoneActive) _zoneActive.Add(origin);
        }
    }

    public override void OnWorldUnload()
    {
        _zones.Clear();
        _hasGem.Clear();
        _zoneActive.Clear();
    }

    public static int TeamDustType(Team team) => team switch
    {
        Team.Red => DustID.RedTorch,
        Team.Green => DustID.GreenTorch,
        Team.Blue => DustID.BlueTorch,
        Team.Yellow => DustID.YellowTorch,
        Team.Pink => DustID.PinkTorch,
        _ => DustID.Torch,
    };

    public static Color TeamColor(Team team) => team switch
    {
        Team.Red => new Color(255, 80, 80),
        Team.Green => new Color(80, 255, 80),
        Team.Blue => new Color(80, 150, 255),
        Team.Yellow => new Color(255, 230, 50),
        Team.Pink => new Color(255, 150, 200),
        _ => Color.White,
    };
}
