using Microsoft.Xna.Framework;
using OxidizedMediumcore.Common.BossOwnership;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using OxidizedMediumcore.Common.Admin;

namespace OxidizedMediumcore.Common.Glory;

public sealed class TeamGlorySystem : ModSystem
{
    public const int BaseHalfW = 33;
    public const int BaseHalfH = 28;
    public const int HardCapHalfW = 200;
    public const int HardCapHalfH = 200;

    private const int GloryPerTileW = 7;
    private const int GloryPerTileH = 7;

    internal const int GloryKillPreHMOptional = 20;
    internal const int GloryKillPreHMGlobal = 45;
    internal const int GloryKillHMOptional = 80;
    internal const int GloryKillHMGlobal = 60;
    internal const int GloryKillMoonLord = 115;

    internal const int HardmodeGloryFloor = 402;

    private readonly int[] _glory = new int[6];

    internal bool _retinazarDead;
    internal bool _spazmatismDead;
    internal bool _destroyerDead;
    internal bool _skeletronPrimeDead;
    internal bool AllMechsDead => _retinazarDead && _spazmatismDead && _destroyerDead && _skeletronPrimeDead;

    private readonly HashSet<int> _defeatedGlobalBosses = [];

    private readonly Dictionary<int, Team> _optionalBossLastKiller = [];
    private readonly Dictionary<int, HashSet<Team>> _optionalBossGloryReceived = [];

    private static HashSet<int>? _allTrackedBossTypes;
    private static HashSet<int> AllTrackedBossTypes => _allTrackedBossTypes ??= BuildAllTracked();

    private static HashSet<int> BuildAllTracked()
    {
        var s = new HashSet<int>(GlobalProgressionBosses);
        s.UnionWith(OptionalBosses);
        s.Add(NPCID.Retinazer);
        s.Add(NPCID.Spazmatism);
        s.Add(NPCID.EaterofWorldsBody);
        s.Add(NPCID.EaterofWorldsTail);
        s.Add(NPCID.TheDestroyerBody);
        s.Add(NPCID.TheDestroyerTail);
        s.Add(NPCID.SkeletronHand);
        s.Add(NPCID.PrimeCannon);
        s.Add(NPCID.PrimeLaser);
        s.Add(NPCID.PrimeSaw);
        s.Add(NPCID.PrimeVice);
        s.Add(NPCID.GolemHead);
        s.Add(NPCID.GolemFistLeft);
        s.Add(NPCID.GolemFistRight);
        s.Add(NPCID.Creeper);
        s.Add(NPCID.MoonLordHand);
        s.Add(NPCID.MoonLordHead);
        s.Add(NPCID.MoonLordLeechBlob);
        s.Add(NPCID.WallofFleshEye);
        s.Add(NPCID.EaterofWorldsHead);
        return s;
    }

    internal static readonly HashSet<int> GlobalProgressionBosses = new()
    {
        NPCID.BrainofCthulhu,
        NPCID.SkeletronHead,
        NPCID.WallofFlesh,
        NPCID.TheDestroyer,
        NPCID.SkeletronPrime,
        NPCID.Plantera,
        NPCID.CultistBoss,
        NPCID.MoonLordCore,
    };

    internal static readonly HashSet<int> OptionalBosses = new()
    {
        NPCID.KingSlime,
        NPCID.Deerclops,
        NPCID.QueenBee,
        NPCID.EyeofCthulhu,
        NPCID.GoblinSummoner,
        NPCID.QueenSlimeBoss,
        NPCID.Golem,
        NPCID.DukeFishron,
        NPCID.HallowBoss,
        NPCID.EaterofWorldsHead,
    };

    internal static readonly HashSet<int> PreHardmodeBosses = new()
    {
        NPCID.KingSlime,
        NPCID.EyeofCthulhu,
        NPCID.EaterofWorldsHead,
        NPCID.BrainofCthulhu,
        NPCID.QueenBee,
        NPCID.Deerclops,
        NPCID.SkeletronHead,
    };

    internal static readonly HashSet<int> HardmodeOptionalBosses = new()
    {
        NPCID.QueenSlimeBoss,
        NPCID.Golem,
        NPCID.DukeFishron,
        NPCID.HallowBoss,
    };

    internal static readonly HashSet<int> MechBossHeads = new()
    {
        NPCID.Retinazer,
        NPCID.Spazmatism,
        NPCID.TheDestroyer,
        NPCID.SkeletronPrime,
    };

    private static readonly HashSet<int> EoWSegmentTypes = new()
    {
        NPCID.EaterofWorldsHead,
        NPCID.EaterofWorldsBody,
        NPCID.EaterofWorldsTail,
    };

    public int GetGlory(Team team)
    {
        int i = (int)team;
        return i >= 0 && i < _glory.Length ? _glory[i] : 0;
    }

    public int GetMaxHalfWidth(Team team)
    {
        int bonus = GetGlory(team) / GloryPerTileW;
        return Math.Min(BaseHalfW + bonus, HardCapHalfW);
    }

    public int GetMaxHalfHeight(Team team)
    {
        int bonus = GetGlory(team) / GloryPerTileH;
        return Math.Min(BaseHalfH + bonus, HardCapHalfH);
    }

    internal bool RegisterMechDeath(int npcType)
    {
        if (npcType == NPCID.Retinazer) _retinazarDead = true;
        if (npcType == NPCID.Spazmatism) _spazmatismDead = true;
        if (npcType == NPCID.TheDestroyer) _destroyerDead = true;
        if (npcType == NPCID.SkeletronPrime) _skeletronPrimeDead = true;
        return AllMechsDead;
    }

    private static int GloryAmountForGlobalBoss(int npcType)
    {
        if (npcType == NPCID.MoonLordCore)
            return GloryKillMoonLord;

        if (PreHardmodeBosses.Contains(npcType) || npcType == NPCID.WallofFlesh)
            return GloryKillPreHMGlobal;

        return GloryKillHMGlobal;
    }

    private static int GloryAmountForOptionalBoss(int npcType)
    {
        if (HardmodeOptionalBosses.Contains(npcType))
            return GloryKillHMOptional;

        return GloryKillPreHMOptional;
    }

    internal bool TryGrantGlobalBossGlory(int npcType)
    {
        if (PreHardmodeBosses.Contains(npcType) && Main.hardMode)
            return false;

        if (!_defeatedGlobalBosses.Add(npcType))
            return false;

        int amount = GloryAmountForGlobalBoss(npcType);
        for (int t = 1; t <= 5; t++)
            AddGlory((Team)t, amount);

        if (npcType == NPCID.WallofFlesh)
            ApplyHardmodeGloryFloor();

        return true;
    }

    private void ApplyHardmodeGloryFloor()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        for (int t = 1; t <= 5; t++)
        {
            _glory[t] = HardmodeGloryFloor;
            if (Main.netMode == NetmodeID.Server)
                BroadcastGlorySync((Team)t);
        }

        string msg = $"[Glory] Hardmode activated! All parties reset to {HardmodeGloryFloor} glory.";
        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(msg), new Color(255, 200, 80) { A = 255 });
    }

    internal bool WasKilledByDifferentTeam(int npcType, Team killer) => _optionalBossLastKiller.TryGetValue(npcType, out Team last) && last != Team.None && last != killer;

    internal void HandleOptionalBossKill(int npcType, Team killer, bool penalize = false)
    {
        if (killer == Team.None)
            return;

        if (PreHardmodeBosses.Contains(npcType) && Main.hardMode)
            return;

        int gloryAmount = GloryAmountForOptionalBoss(npcType);

        if (penalize &&
            _optionalBossLastKiller.TryGetValue(npcType, out Team lastKiller) &&
            lastKiller != Team.None && lastKiller != killer)
        {
            if (_optionalBossGloryReceived.TryGetValue(npcType, out var prevReceivers))
            {
                foreach (Team prev in prevReceivers)
                    AddGlory(prev, -gloryAmount, broadcast: false);
            }
            _optionalBossGloryReceived.Remove(npcType);
            BroadcastOptionalBossReset(killer, lastKiller, npcType);
        }

        _optionalBossLastKiller[npcType] = killer;

        if (!_optionalBossGloryReceived.TryGetValue(npcType, out var received))
        {
            received = new HashSet<Team>();
            _optionalBossGloryReceived[npcType] = received;
        }

        if (received.Add(killer))
            AddGlory(killer, gloryAmount);
    }

    private static void BroadcastOptionalBossReset(Team newKiller, Team prevKiller, int npcType)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        string bossName = Lang.GetNPCNameValue(npcType);
        string msg = $"[Glory] {TeamName(newKiller)} party stole {bossName} from {TeamName(prevKiller)} party — glory has been transferred!";
        Color col = Gemlocks.GemLockZones.TeamColor(newKiller) * 1.2f;
        col.A = 255;
        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(msg), col);
    }

    internal void AddGlory(Team team, int amount, bool broadcast = true)
    {
        int i = (int)team;
        if (i <= 0 || i >= _glory.Length)
            return;

        _glory[i] = Math.Max(0, _glory[i] + amount);
        if (Main.netMode == NetmodeID.Server)
            BroadcastGlorySync(team);
        if (broadcast && amount != 0)
            AnnounceGloryChange(team, amount);

        if (amount < 0)
            ModContent.GetInstance<Gemlocks.GemLockZones>().EnforceLimitsForTeam(team);
    }

    private static void AnnounceGloryChange(Team team, int delta)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        string sign = delta > 0 ? "+" : "";
        string msg = $"[Glory] {TeamName(team)} party: {sign}{delta} glory";
        Color col = Gemlocks.GemLockZones.TeamColor(team) * 1.2f;
        col.A = 255;
        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(msg), col);
    }

    private void BroadcastGlorySync(Team team, int ignoreClient = -1)
    {
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        var packet = mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.GlorySync);
        packet.Write((byte)team);
        packet.Write(_glory[(int)team]);
        packet.Send(toClient: -1, ignoreClient: ignoreClient);
    }

    public void SendFullGlorySyncTo(int toClient)
    {
        var mod = ModContent.GetInstance<OxidizedMediumcore>();
        for (int t = 1; t <= 5; t++)
        {
            var packet = mod.GetPacket();
            packet.Write((byte)OxidizedMediumcore.PacketID.GlorySync);
            packet.Write((byte)t);
            packet.Write(_glory[t]);
            packet.Send(toClient);
        }
    }

    public static void HandleGlorySync(BinaryReader reader, int whoAmI)
    {
        var team = (Team)reader.ReadByte();
        int glory = reader.ReadInt32();
        int i = (int)team;
        
        if (Main.netMode == NetmodeID.Server)
        {
            if (!Main.player[whoAmI].GetModPlayer<AdminPlayer>().IsAdmin)
                return;

            var inst = ModContent.GetInstance<TeamGlorySystem>();
            if (i >= 0 && i < inst._glory.Length)
                inst._glory[i] = glory;

            inst.BroadcastGlorySync(team, ignoreClient: whoAmI);
        }
        else
        {
            var inst = ModContent.GetInstance<TeamGlorySystem>();
            if (i >= 0 && i < inst._glory.Length)
                inst._glory[i] = glory;
        }
    }

    /// <summary>
    /// Returns true if there are no more active EoW segments of any kind in the world.
    /// </summary>
    internal static bool IsLastEoWSegment()
    {
        foreach (var npc in Main.npc)
        {
            if (npc.active && EoWSegmentTypes.Contains(npc.type))
                return false;
        }
        return true;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["glory"] = new List<int>(_glory);
        tag["retinazarDead"] = _retinazarDead;
        tag["spazmatismDead"] = _spazmatismDead;
        tag["destroyerDead"] = _destroyerDead;
        tag["skeletronPrimeDead"] = _skeletronPrimeDead;
        tag["defeatedGlobalBosses"] = new List<int>(_defeatedGlobalBosses);

        var bossIds = new List<int>();
        var bossTeams = new List<int>();
        foreach (var (npcType, team) in _optionalBossLastKiller)
        {
            bossIds.Add(npcType);
            bossTeams.Add((int)team);
        }
        tag["optBossIds"] = bossIds;
        tag["optBossTeams"] = bossTeams;

        var grBossIds = new List<int>();
        var grTeamLists = new List<int[]>();
        foreach (var (npcType, teams) in _optionalBossGloryReceived)
        {
            grBossIds.Add(npcType);
            var arr = new int[teams.Count];
            int idx = 0;
            foreach (var t in teams) arr[idx++] = (int)t;
            grTeamLists.Add(arr);
        }
        var flat = new List<int>();
        for (int i = 0; i < grBossIds.Count; i++)
        {
            flat.Add(grBossIds[i]);
            flat.Add(grTeamLists[i].Length);
            foreach (var t in grTeamLists[i]) flat.Add(t);
        }
        tag["optGloryFlat"] = flat;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (!tag.ContainsKey("glory"))
            return;

        var list = tag.GetList<int>("glory");
        for (int i = 0; i < _glory.Length && i < list.Count; i++)
            _glory[i] = list[i];

        _retinazarDead = tag.GetBool("retinazarDead");
        _spazmatismDead = tag.GetBool("spazmatismDead");
        _destroyerDead = tag.GetBool("destroyerDead");
        _skeletronPrimeDead = tag.GetBool("skeletronPrimeDead");

        _defeatedGlobalBosses.Clear();
        if (tag.ContainsKey("defeatedGlobalBosses"))
            foreach (int id in tag.GetList<int>("defeatedGlobalBosses"))
                _defeatedGlobalBosses.Add(id);

        _optionalBossLastKiller.Clear();
        if (tag.ContainsKey("optBossIds"))
        {
            var ids = tag.GetList<int>("optBossIds");
            var teams = tag.GetList<int>("optBossTeams");
            for (int i = 0; i < ids.Count; i++)
                _optionalBossLastKiller[ids[i]] = (Team)teams[i];
        }

        _optionalBossGloryReceived.Clear();
        if (tag.ContainsKey("optGloryFlat"))
        {
            var flat = tag.GetList<int>("optGloryFlat");
            int i = 0;
            while (i < flat.Count)
            {
                int npcType = flat[i++];
                int count = flat[i++];
                var set = new HashSet<Team>();
                for (int j = 0; j < count; j++)
                    set.Add((Team)flat[i++]);
                _optionalBossGloryReceived[npcType] = set;
            }
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        for (int i = 0; i < _glory.Length; i++) writer.Write(_glory[i]);
        writer.Write(_retinazarDead);
        writer.Write(_spazmatismDead);
        writer.Write(_destroyerDead);
        writer.Write(_skeletronPrimeDead);

        writer.Write(_defeatedGlobalBosses.Count);
        foreach (var b in _defeatedGlobalBosses) writer.Write(b);

        writer.Write(_optionalBossLastKiller.Count);
        foreach (var kvp in _optionalBossLastKiller)
        {
            writer.Write(kvp.Key);
            writer.Write((byte)kvp.Value);
        }

        writer.Write(_optionalBossGloryReceived.Count);
        foreach (var kvp in _optionalBossGloryReceived)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value.Count);
            foreach (var t in kvp.Value) writer.Write((byte)t);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        for (int i = 0; i < _glory.Length; i++) _glory[i] = reader.ReadInt32();
        _retinazarDead = reader.ReadBoolean();
        _spazmatismDead = reader.ReadBoolean();
        _destroyerDead = reader.ReadBoolean();
        _skeletronPrimeDead = reader.ReadBoolean();

        _defeatedGlobalBosses.Clear();
        int count1 = reader.ReadInt32();
        for (int i = 0; i < count1; i++) _defeatedGlobalBosses.Add(reader.ReadInt32());

        _optionalBossLastKiller.Clear();
        int count2 = reader.ReadInt32();
        for (int i = 0; i < count2; i++) _optionalBossLastKiller[reader.ReadInt32()] = (Team)reader.ReadByte();

        _optionalBossGloryReceived.Clear();
        int count3 = reader.ReadInt32();
        for (int i = 0; i < count3; i++)
        {
            int key = reader.ReadInt32();
            int subCount = reader.ReadInt32();
            var set = new HashSet<Team>();
            for (int j = 0; j < subCount; j++) set.Add((Team)reader.ReadByte());
            _optionalBossGloryReceived[key] = set;
        }
    }

    public override void OnWorldUnload()
    {
        Array.Clear(_glory, 0, _glory.Length);
        _retinazarDead = false;
        _spazmatismDead = false;
        _destroyerDead = false;
        _skeletronPrimeDead = false;
        _defeatedGlobalBosses.Clear();
        _optionalBossLastKiller.Clear();
        _optionalBossGloryReceived.Clear();
        _allTrackedBossTypes = null;
    }

    internal static bool AnyActiveOnTeam(Team team)
    {
        foreach (var p in Main.player)
            if (p.active && p.team == (int)team)
                return true;

        return false;
    }

    internal static string TeamName(Team t) => t switch
    {
        Team.Red => "Red",
        Team.Green => "Green",
        Team.Blue => "Blue",
        Team.Yellow => "Yellow",
        Team.Pink => "Pink",
        _ => "Unknown",
    };

    internal static bool IsTrackedBossType(int npcType) => AllTrackedBossTypes.Contains(npcType);
}
