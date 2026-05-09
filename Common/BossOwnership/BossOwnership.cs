using Microsoft.Xna.Framework;
using OxidizedMediumcore.Common.Glory;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.BossOwnership;

public sealed class BossOwnershipSystem : ModSystem
{
    private readonly Dictionary<int, Team> _bossOwners = [];
    private readonly Dictionary<int, int> _ownerDeadTicks = [];

    private const int StealDelayTicks = 5 * 60;
    private const float StealProximityTiles = 80f;

    private static readonly HashSet<int> GlobalBossTypes =
    [
        NPCID.WallofFlesh,
        NPCID.WallofFleshEye,
        NPCID.MoonLordCore,
        NPCID.MoonLordHand,
        NPCID.MoonLordHead,
        NPCID.MoonLordLeechBlob,
    ];

    private static readonly HashSet<int> WormSegmentTypes =
    [
        NPCID.EaterofWorldsBody,
        NPCID.EaterofWorldsTail,
        NPCID.TheDestroyerBody,
        NPCID.TheDestroyerTail,
    ];

    public Team GetOwner(int npcWhoAmI) => _bossOwners.TryGetValue(npcWhoAmI, out var t) ? t : Team.None;

    public void RegisterBoss(NPC npc, Team owner)
    {
        if (IsGlobalBoss(npc.type))
            return;

        _bossOwners[npc.whoAmI] = owner;
        _ownerDeadTicks.Remove(npc.whoAmI);
        BossOwnershipSync.BroadcastOwnership(npc, owner, isSteal: false);
    }

    public void UnregisterBoss(int npcWhoAmI)
    {
        _bossOwners.Remove(npcWhoAmI);
        _ownerDeadTicks.Remove(npcWhoAmI);
    }

    public static bool IsGlobalBoss(int npcType) => GlobalBossTypes.Contains(npcType);
    public static bool IsWormSegment(int npcType) => WormSegmentTypes.Contains(npcType);

    public bool CanTeamAttack(NPC npc, Team attackerTeam)
    {
        if (!npc.boss && !IsBossSegment(npc.type))
            return true;

        if (IsGlobalBoss(npc.type))
            return true;

        if (!_bossOwners.TryGetValue(npc.whoAmI, out var owner))
        {
            if (attackerTeam != Team.None)
            {
                _bossOwners[npc.whoAmI] = attackerTeam;
                _ownerDeadTicks.Remove(npc.whoAmI);
                BossOwnershipSync.BroadcastOwnership(npc, attackerTeam, isSteal: false);
            }
            return true;
        }

        if (owner == Team.None)
            return true;

        if (owner == attackerTeam)
            return true;

        bool ownerAlive = Main.player.Any(p => p.active && !p.dead && p.team == (int)owner);

        if (!ownerAlive)
        {
            _bossOwners[npc.whoAmI] = attackerTeam;
            _ownerDeadTicks.Remove(npc.whoAmI);
            BossOwnershipSync.BroadcastOwnership(npc, attackerTeam, isSteal: true);
            return true;
        }

        return false;
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        List<int> toRemove = [];

        foreach (var key in _bossOwners.Keys)
        {
            var npc = Main.npc[key];
            if (!npc.active || !npc.boss && !IsBossSegment(npc.type))
            {
                toRemove.Add(key);
                continue;
            }

            TickFailsafe(npc);
        }

        foreach (var k in toRemove)
            UnregisterBoss(k);
    }

    private void TickFailsafe(NPC npc)
    {
        if (!_bossOwners.TryGetValue(npc.whoAmI, out var owner))
            return;

        if (owner == Team.None)
            return;

        bool ownerAlive = Main.player.Any(p => p.active && !p.dead && p.team == (int)owner);

        if (ownerAlive)
        {
            _ownerDeadTicks.Remove(npc.whoAmI);
            return;
        }

        Team? nearbyEnemy = FindNearbyEnemyTeam(npc, owner);
        if (nearbyEnemy is null)
        {
            _ownerDeadTicks.Remove(npc.whoAmI);
            return;
        }

        _ownerDeadTicks.TryGetValue(npc.whoAmI, out int ticks);
        ticks++;
        _ownerDeadTicks[npc.whoAmI] = ticks;

        if (ticks < StealDelayTicks)
            return;

        _bossOwners[npc.whoAmI] = nearbyEnemy.Value;
        _ownerDeadTicks.Remove(npc.whoAmI);
        BossOwnershipSync.BroadcastOwnership(npc, nearbyEnemy.Value, isSteal: true);
    }

    private static Team? FindNearbyEnemyTeam(NPC npc, Team ownerTeam)
    {
        Team? best = null;
        float bestDist = float.MaxValue;

        foreach (var p in Main.player)
        {
            if (!p.active || p.dead)
                continue;
            if (p.team == (int)ownerTeam || p.team == (int)Team.None)
                continue;

            float dist = Vector2.Distance(p.Center, npc.Center) / 16f;
            if (dist > StealProximityTiles)
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = (Team)p.team;
            }
        }

        return best;
    }

    public override void OnWorldUnload()
    {
        _bossOwners.Clear();
        _ownerDeadTicks.Clear();
    }

    public static bool IsBossSegment(int type) => type is
        NPCID.TheDestroyerBody or NPCID.TheDestroyerTail or
        NPCID.Retinazer or NPCID.Spazmatism or
        NPCID.SkeletronHead or NPCID.SkeletronHand or
        NPCID.SkeletronPrime or
        NPCID.PrimeCannon or NPCID.PrimeLaser or NPCID.PrimeSaw or NPCID.PrimeVice or
        NPCID.GolemHead or NPCID.GolemFistLeft or NPCID.GolemFistRight or
        NPCID.DungeonGuardian or
        NPCID.BrainofCthulhu or NPCID.Creeper or
        NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail;

    public static Color TeamColor(Team t) => t switch
    {
        Team.Red => new Color(255, 80, 80),
        Team.Green => new Color(80, 255, 80),
        Team.Blue => new Color(80, 150, 255),
        Team.Yellow => new Color(255, 230, 50),
        Team.Pink => new Color(255, 150, 200),
        _ => Color.White,
    };
}

public sealed class BossOwnershipNPC : GlobalNPC
{
    public Team LastOwner = Team.None;

    public override bool InstancePerEntity => true;

    public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        if (!npc.boss && !IsTrackedSegment(npc.type))
            return;

        if (BossOwnershipSystem.IsGlobalBoss(npc.type))
            return;

        Player? closest = null;
        float bestDist = float.MaxValue;
        foreach (var p in Main.player)
        {
            if (!p.active || p.dead || p.team == (int)Team.None)
                continue;
            float d = Vector2.Distance(p.Center, npc.Center);
            if (d < bestDist) { bestDist = d; closest = p; }
        }

        if (closest is not null)
            ModContent.GetInstance<BossOwnershipSystem>().RegisterBoss(npc, (Team)closest.team);
    }

    public override bool? CanBeHitByItem(NPC npc, Player player, Item item)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return null;

        if (!npc.boss && !IsTrackedSegment(npc.type))
            return null;

        return CanAttack(npc, (Team)player.team) ? null : false;
    }

    public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return null;

        if (!npc.boss && !IsTrackedSegment(npc.type))
            return null;

        if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
            return null;

        var player = Main.player[projectile.owner];
        if (!player.active)
            return null;

        return CanAttack(npc, (Team)player.team) ? null : false;
    }

    private static bool CanAttack(NPC npc, Team attackerTeam)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return true;

        if (Main.netMode == NetmodeID.Server)
            return ModContent.GetInstance<BossOwnershipSystem>().CanTeamAttack(npc, attackerTeam);

        if (BossOwnershipSystem.IsGlobalBoss(npc.type))
            return true;

        var owner = ModContent.GetInstance<BossOwnershipClient>().GetOwner(npc.whoAmI);
        if (owner == Team.None)
            return true;

        return owner == attackerTeam;
    }

    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        LastOwner = ModContent.GetInstance<BossOwnershipSystem>().GetOwner(npc.whoAmI);
        ModContent.GetInstance<BossOwnershipSystem>().UnregisterBoss(npc.whoAmI);
    }

    private static bool IsTrackedSegment(int type) => type is
        NPCID.TheDestroyerBody or NPCID.TheDestroyerTail or
        NPCID.Retinazer or
        NPCID.SkeletronHead or NPCID.SkeletronHand or
        NPCID.SkeletronPrime or
        NPCID.PrimeCannon or NPCID.PrimeLaser or NPCID.PrimeSaw or NPCID.PrimeVice or
        NPCID.GolemHead or NPCID.GolemFistLeft or NPCID.GolemFistRight or
        NPCID.BrainofCthulhu or NPCID.Creeper or
        NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail;
}