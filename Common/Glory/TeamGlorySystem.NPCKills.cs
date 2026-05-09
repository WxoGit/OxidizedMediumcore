using Microsoft.Xna.Framework;
using OxidizedMediumcore.Common.BossOwnership;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.Glory;

public sealed class TeamGloryNPCHooks : GlobalNPC
{
    public override bool InstancePerEntity => true;

    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (!TeamGlorySystem.IsTrackedBossType(npc.type))
            return;

        var inst = ModContent.GetInstance<TeamGlorySystem>();

        if (TeamGlorySystem.MechBossHeads.Contains(npc.type))
        {
            if (!inst.RegisterMechDeath(npc.type))
                return;

            inst.TryGrantGlobalBossGlory(NPCID.TheDestroyer);
            return;
        }

        if (TeamGlorySystem.GlobalProgressionBosses.Contains(npc.type))
        {
            inst.TryGrantGlobalBossGlory(npc.type);
            return;
        }

        // only grant glory when the LAST segment dies
        if (npc.type == NPCID.EaterofWorldsHead ||
            npc.type == NPCID.EaterofWorldsBody ||
            npc.type == NPCID.EaterofWorldsTail)
        {
            bool isLast = true;
            foreach (var other in Main.npc)
            {
                if (!other.active) continue;
                if (other.whoAmI == npc.whoAmI) continue;
                if (other.type == NPCID.EaterofWorldsHead ||
                    other.type == NPCID.EaterofWorldsBody ||
                    other.type == NPCID.EaterofWorldsTail)
                {
                    isLast = false;
                    break;
                }
            }

            if (!isLast)
                return;

            var ownershipNPC = npc.GetGlobalNPC<BossOwnershipNPC>();
            Team killer = ownershipNPC.LastOwner != Team.None
                ? ownershipNPC.LastOwner
                : FindClosestActiveTeam(npc);

            bool penalize = inst.WasKilledByDifferentTeam(NPCID.EaterofWorldsHead, killer);
            inst.HandleOptionalBossKill(NPCID.EaterofWorldsHead, killer, penalize: penalize);
            return;
        }

        if (TeamGlorySystem.OptionalBosses.Contains(npc.type))
        {
            var ownershipNPC = npc.GetGlobalNPC<BossOwnershipNPC>();
            Team killer = ownershipNPC.LastOwner != Team.None
                ? ownershipNPC.LastOwner
                : FindClosestActiveTeam(npc);

            bool penalize = inst.WasKilledByDifferentTeam(npc.type, killer);
            inst.HandleOptionalBossKill(npc.type, killer, penalize: penalize);
            return;
        }
    }

    private static Team FindClosestActiveTeam(NPC npc)
    {
        Team best = Team.None;
        float bestDist = float.MaxValue;
        foreach (var p in Main.player)
        {
            if (!p.active || p.dead || p.team == (int)Team.None)
                continue;
            float d = Vector2.Distance(p.Center, npc.Center);
            if (d < bestDist) { bestDist = d; best = (Team)p.team; }
        }
        return best;
    }
}
