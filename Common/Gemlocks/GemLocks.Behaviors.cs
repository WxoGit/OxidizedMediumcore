using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using OxidizedMediumcore.Common.Admin;
using System;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.Gemlocks;

public sealed class GemLockTile : GlobalTile
{
    public override void Load() => On_Player.TileInteractionsUse += On_TileInteractionsUse;

    public override void Unload() => On_Player.TileInteractionsUse -= On_TileInteractionsUse;

    private static void On_TileInteractionsUse(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            orig(self, myX, myY);
            return;
        }

        if (Main.netMode != NetmodeID.Server && Main.tile[myX, myY].type == TileID.GemLocks)
        {
            var origin = GemLockHelper.GetOrigin(myX, myY);
            if (GemLockHelper.HasGem(origin) && GemLockHelper.GetTeamForOrigin(origin) != Team.None)
            {
                if (Main.mouseRight && Main.mouseRightRelease)
                {
                    var radial = ModContent.GetInstance<GemLockRadialSystem>();
                    if (radial.Active && radial.TargetOrigin == origin)
                        radial.Active = false;
                    else
                    {
                        radial.Active = false;
                        radial.Open(origin.X, origin.Y);
                    }
                }

                return;
            }
        }

        orig(self, myX, myY);
    }

    public override bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return base.TileFrame(i, j, type, ref resetFrame, ref noBreak);

        if (type == TileID.GemLocks && Main.netMode != NetmodeID.MultiplayerClient)
            ModContent.GetInstance<GemLockZones>().RefreshLock(GemLockHelper.GetOrigin(i, j));
        return base.TileFrame(i, j, type, ref resetFrame, ref noBreak);
    }

    public override void RightClick(int i, int j, int type)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        if (Main.netMode == NetmodeID.Server)
            return;

        Team required = GemLockHelper.GetRequiredTeam(i, j, type);
        if (required == Team.None || (Team)Main.LocalPlayer.team == required)
            return;

        var origin = GemLockHelper.GetOrigin(i, j);
        if (!ModContent.GetInstance<GemLockZones>().IsProtectionEnabled(origin))
            return;

        for (int d = 0; d < 8; d++)
            Dust.NewDust(new Vector2(i * 16, j * 16), 16, 16, DustID.Torch, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));

        Terraria.Audio.SoundEngine.PlaySound(SoundID.Unlock, Main.LocalPlayer.position);
    }

    public override void MouseOver(int i, int j, int type)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        if (Main.netMode == NetmodeID.Server)
            return;

        Team required = GemLockHelper.GetRequiredTeam(i, j, type);
        if (required == Team.None)
            return;

        var origin = GemLockHelper.GetOrigin(i, j);
        bool protEnabled = ModContent.GetInstance<GemLockZones>().IsProtectionEnabled(origin);

        Main.LocalPlayer.noThrow = 2;
        Main.LocalPlayer.cursorItemIconEnabled = true;

        Main.LocalPlayer.cursorItemIconID =
            !protEnabled || (Team)Main.LocalPlayer.team == required
                ? ItemID.GoldenKey
                : ItemID.ChestLock;
    }
}

public sealed class GemLockBuildBlock : ModPlayer
{
    public override void PostUpdateMiscEffects()
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        if (!Player.ItemAnimationActive)
            return;

        var held = Player.HeldItem;
        if (held.createTile < 0 && held.createWall < 0)
            return;

        if (ModContent.GetInstance<GemLockZones>().IsInsideEnemyZone(Player.tileTargetX, Player.tileTargetY, (Team)Player.team))
            Player.noBuilding = true;
    }
}

public sealed class GemLockMiningProtection : GlobalTile
{
    public override void Load() => IL_Player.PickTile += IL_Player_PickTile;

    private void IL_Player_PickTile(ILContext il)
    {
        var c = new ILCursor(il);

        if (!c.TryGotoNext(MoveType.After, i => i.MatchCall<Player>(nameof(Player.GetPickaxeDamage))))
            throw new Exception("Oxidized Mediumcore: couldnt apply mining protections");

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<int, Player, int>>(static (damage, player) =>
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return damage;

            if (!ModContent.GetInstance<GemLockZones>().IsInsideEnemyZone(Player.tileTargetX, Player.tileTargetY, (Team)player.team))
                return damage;

            return (int)(damage * (1f - AdminConfig.PickSpeedPenalty));
        });
    }
}

public sealed class GemLockMiningFatigue : ModPlayer
{
    public override void PostUpdateMiscEffects() 
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;
    }
}

public sealed class GemLockBlastReduction : ModSystem
{
    public override void Load() => On_Projectile.ExplodeTiles += OnExplodeTiles;
    public override void Unload() => On_Projectile.ExplodeTiles -= OnExplodeTiles;

    private static void OnExplodeTiles(On_Projectile.orig_ExplodeTiles orig, Projectile self, Vector2 center, int explosionRadius, int minTileX, int maxTileX, int minTileY, int maxTileY, bool wallExplosion)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            orig(self, center, explosionRadius, minTileX, maxTileX, minTileY, maxTileY, wallExplosion);
            return;
        }

        Team ownerTeam = self.owner >= 0 && self.owner < Main.maxPlayers
            ? (Team)Main.player[self.owner].team
            : Team.None;

        if (!ModContent.GetInstance<GemLockZones>().IsInsideEnemyZone((int)(center.X / 16), (int)(center.Y / 16), ownerTeam))
        {
            orig(self, center, explosionRadius, minTileX, maxTileX, minTileY, maxTileY, wallExplosion);
            return;
        }

        float multiplier = AdminConfig.BlastRadiusMultiplier;
        if (multiplier <= 0)
            return;

        if (multiplier == 1)
        {
            orig(self, center, explosionRadius, minTileX, maxTileX, minTileY, maxTileY, wallExplosion);
            return;
        }

        int reduced = (int)(explosionRadius * (1f - multiplier));
        int newMinX = (int)(center.X / 16f - reduced);
        int newMaxX = (int)(center.X / 16f + reduced);
        int newMinY = (int)(center.Y / 16f - reduced);
        int newMaxY = (int)(center.Y / 16f + reduced);
        Utils.ClampWithinWorld(ref newMinX, ref newMinY, ref newMaxX, ref newMaxY);

        orig(self, center, reduced, newMinX, newMaxX, newMinY, newMaxY, wallExplosion);
    }
}