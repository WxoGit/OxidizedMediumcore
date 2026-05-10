using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OxidizedMediumcore.Common.Gemlocks;
using OxidizedMediumcore.Common.Glory;
using OxidizedMediumcore.Core;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace OxidizedMediumcore.Common.Gemlocks;

public sealed class GemLockRadialSystem : ModSystem
{
    public enum Option { None = -1, RemoveGem = 0, Resize = 1, ToggleProtection = 2 }

    public bool Active;
    public Point TargetOrigin;
    public bool ResizeMode;

    private bool _onMenu;
    private int _resizePreviewHW;
    private int _resizePreviewHH;
    private int _resizeAnimTimer;
    private float _openAnimTimer;
    private int _resizeGloryCapW;
    private int _resizeGloryCapH;

    // maybe this could have done without arrays using only globaltime?? but mehh lul
    private readonly float[] _buttonScale = new float[3];
    private readonly float[] _breathTimer = new float[3];

    private Vector2 TileScreenPos => new Vector2((TargetOrigin.X + 1.5f) * 16f, (TargetOrigin.Y + 1.5f) * 16f) - Main.screenPosition;
    private Vector2 TileWorldCenter => new Vector2((TargetOrigin.X + 1.5f) * 16f, (TargetOrigin.Y + 1.5f) * 16f);

    public void Open(int originX, int originY)
    {
        if (!Active) SoundEngine.PlaySound(SoundID.Item129);

        Active = true;
        ResizeMode = false;
        TargetOrigin = new Point(originX, originY);
        _openAnimTimer = 0f;

        for (int i = 0; i < 3; i++)
        {
            _buttonScale[i] = 0f;
            _breathTimer[i] = i * (MathF.PI * 2f / 3);
        }
    }

    private Vector2 GetOptionPos(Option opt)
    {
        float t = _openAnimTimer / 12f;
        t = 1f - (1f - t) * (1f - t) * (1f - t);

        float angle = -MathF.PI / 2f + (int)opt * (2f * MathF.PI / 3f);
        Vector2 target = TileScreenPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 65f;
        return Vector2.Lerp(TileScreenPos, target, t);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        int idx = layers.FindIndex(l => l.Name == "Vanilla: Wire Selection");
        if (idx < 0)
            idx = layers.Count;

        layers.Insert(idx, new LegacyGameInterfaceLayer("OxidizedMediumcore: GemLock Radial", () => { Draw(Main.spriteBatch); return true; }, InterfaceScaleType.Game));
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        if (!Active)
            return;

        if (!ResizeMode)
            _openAnimTimer = Math.Min(_openAnimTimer + 1f, 12f);

        Player player = Main.LocalPlayer;

        if (ResizeMode)
        {
            _resizeAnimTimer++;
            _onMenu = true;
            player.mouseInterface = true;

            Vector2 worldMouse = Main.MouseWorld;
            Vector2 center = TileWorldCenter;

            int hw = Utils.Clamp((int)MathF.Abs(worldMouse.X - center.X) / 16, GemLockZones.MinHalf, _resizeGloryCapW);
            int hh = Utils.Clamp((int)MathF.Abs(worldMouse.Y - center.Y) / 16, GemLockZones.MinHalf, _resizeGloryCapH);

            _resizePreviewHW = hw;
            _resizePreviewHH = hh;

            if (Main.mouseRight && Main.mouseRightRelease)
            {
                ResizeMode = false;
                Active = false;
                return;
            }

            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                ResizeMode = false;
                Active = false;
                return;
            }

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Team lockTeam = GemLockHelper.GetTeamForOrigin(TargetOrigin);
                bool allowed = lockTeam == Team.None || (Team)player.team == lockTeam;
                if (allowed)
                    ModContent.GetInstance<GemLockZones>().SetZone(TargetOrigin, _resizePreviewHW, _resizePreviewHH);
                ResizeMode = false;
                Active = false;
            }

            return;
        }

        if (player.dead || Main.mouseItem.type > 0 || player.mouseInterface && !_onMenu)
        {
            Active = false;
            return;
        }

        _onMenu = false;
    }

    private bool IsTeamRestricted(Option opt)
    {
        Team lockTeam = GemLockHelper.GetTeamForOrigin(TargetOrigin);
        if (lockTeam == Team.None)
            return false;

        bool isOwnTeam = (Team)Main.LocalPlayer.team == lockTeam;

        var zones = ModContent.GetInstance<GemLockZones>();
        bool activeElsewhere = !zones.IsZoneEnabled(TargetOrigin) && zones.HasActiveZoneElsewhere(TargetOrigin);
        bool enemyConflict = !zones.IsZoneEnabled(TargetOrigin) && zones.HasOverlappingEnemyZone(TargetOrigin);
        bool tryingToEnable = zones.IsZoneEnabled(TargetOrigin) && !zones.IsProtectionEnabled(TargetOrigin);

        return opt switch
        {
            Option.Resize => !isOwnTeam,
            Option.ToggleProtection => (!isOwnTeam && !zones.IsZoneEnabled(TargetOrigin))
                                       || (!isOwnTeam && tryingToEnable)
                                       || activeElsewhere
                                       || enemyConflict,
            _ => false,
        };
    }

    private void Draw(SpriteBatch sb)
    {
        if (!Active)
            return;

        if (ResizeMode)
        {
            DrawResizeOverlay(sb);
            return;
        }

        Vector2 mouse = Main.MouseScreen;
        bool anyHovered = false;
        _onMenu = false;

        float t = _openAnimTimer / 12f;
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);

        bool[] isHovered = new bool[3];
        bool anyHoveredCheck = false;
        for (int i = 0; i < 3; i++)
        {
            Option opt = (Option)i;
            bool restricted = IsTeamRestricted(opt);
            Vector2 iconPos = GetOptionPos(opt);
            bool hov = !restricted && eased > 0.85f && Vector2.Distance(iconPos, mouse) < 19f;
            if (anyHoveredCheck) hov = false;
            if (hov) anyHoveredCheck = true;
            isHovered[i] = hov;
        }

        for (int i = 0; i < 3; i++)
        {
            float scaleTarget = isHovered[i] ? 1.22f : 1f;
            _buttonScale[i] += (scaleTarget - _buttonScale[i]) * 0.18f;
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        for (int i = 0; i < 3; i++)
        {
            Option opt = (Option)i;
            Vector2 iconPos = GetOptionPos(opt);
            bool restricted = IsTeamRestricted(opt);
            bool hovered = isHovered[i];

            if (hovered && !anyHovered) anyHovered = true;

            Color tint = restricted ? new Color(80, 80, 80, 160) : Color.White;
            tint *= eased;

            float combinedScale = eased * _buttonScale[i];

            Texture2D bg = TextureAssets.WireUi[hovered ? 1 : 0].Value;
            Texture2D icon = IconTexture(opt);

            sb.Draw(bg, iconPos, null, tint, 0f, bg.Size() / 2f, combinedScale, SpriteEffects.None, 0f);
            sb.Draw(icon, iconPos, null, tint, 0f, icon.Size() / 2f, (opt == Option.RemoveGem ? 0.78f : 1f) * combinedScale, SpriteEffects.None, 0f);

            if (hovered)
            {
                _onMenu = true;
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                    ExecuteOption(opt);
            }
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

        var font = FontAssets.MouseText.Value;

        for (int i = 0; i < 3; i++)
        {
            Option opt = (Option)i;

            if (eased < 1f) continue;

            string gloryCapStr = "";
            if (opt == Option.Resize && !IsTeamRestricted(opt))
            {
                Team lockTeam = GemLockHelper.GetTeamForOrigin(TargetOrigin);
                if (lockTeam != Team.None)
                {
                    var glory = ModContent.GetInstance<TeamGlorySystem>();
                    int capW = glory.GetMaxHalfWidth(lockTeam);
                    int capH = glory.GetMaxHalfHeight(lockTeam);
                    int pts = glory.GetGlory(lockTeam);
                    gloryCapStr = $" (max {capW * 2 + 1}x{capH * 2 + 1} | {pts} glory)";
                }
            }

            var zones = ModContent.GetInstance<GemLockZones>();
            string optLabel = opt switch
            {
                Option.RemoveGem => "Remove Gem",
                Option.Resize => IsTeamRestricted(opt)
                    ? "Resize  (party only)"
                    : $"Resize{gloryCapStr}",
                Option.ToggleProtection =>
                    zones.HasActiveZoneElsewhere(TargetOrigin)
                        ? "Already active elsewhere"
                    : (!zones.IsZoneEnabled(TargetOrigin) && zones.HasOverlappingEnemyZone(TargetOrigin))
                        ? "Blocked by enemy zone"
                    : !zones.IsZoneEnabled(TargetOrigin)
                        ? "Activate Zone (party only)"
                    : zones.IsProtectionEnabled(TargetOrigin)
                        ? "Disable Protection"
                    : (GemLockHelper.GetTeamForOrigin(TargetOrigin) != Team.None
                        && (Team)Main.LocalPlayer.team != GemLockHelper.GetTeamForOrigin(TargetOrigin))
                        ? "Enable Protection  (party only)"
                        : "Enable Protection",
                _ => ""
            };

            Vector2 iconPos = GetOptionPos(opt);
            Vector2 labelSize = font.MeasureString(optLabel) * 0.5f;
            Vector2 labelPos = iconPos + new Vector2(-labelSize.X / 2f, 26f * _buttonScale[i]);
            Color labelColor = IsTeamRestricted(opt) ? new Color(130, 130, 130) : Color.White;
            Utils.DrawBorderString(sb, optLabel, labelPos, labelColor, 0.5f);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        if (Main.mouseLeft && Main.mouseLeftRelease && !anyHovered)
            Active = false;
    }

    private void DrawResizeOverlay(SpriteBatch sb)
    {
        Color teamColor = GemLockZones.TeamColor(GemLockHelper.GetTeamForOrigin(TargetOrigin));

        int cx = TargetOrigin.X + 1;
        int cy = TargetOrigin.Y + 1;

        var world = new Rectangle((cx - _resizePreviewHW) * 16, (cy - _resizePreviewHH) * 16, (_resizePreviewHW * 2 + 1) * 16, (_resizePreviewHH * 2 + 1) * 16);

        float animT = Utils.GetLerpValue(0, 15, _resizeAnimTimer, clamped: true);
        animT = 1f - (1f - animT) * (1f - animT) * (1f - animT);

        int sw = (int)(world.Width * animT) / 16 * 16;
        int sh = (int)(world.Height * animT) / 16 * 16;

        var dest = new Rectangle(
            world.X + (world.Width - sw) / 2 - (int)Main.screenPosition.X,
            world.Y + (world.Height - sh) / 2 - (int)Main.screenPosition.Y,
            sw, sh);

        float tileBorderT = Utils.GetLerpValue(15, 30, _resizeAnimTimer, clamped: true);
        tileBorderT *= (1f - tileBorderT) * 4f;

        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        var shader = Assets.GemZone.Createp0();
        shader.Parameters.globalTime = (float)Main.timeForVisualEffects;
        shader.Parameters.teamColor = teamColor.ToVector3();
        shader.Parameters.baseTexture = new HlslSampler2D { Texture = Assets.InvisPixel.Asset.Value, Sampler = SamplerState.LinearClamp };
        shader.Parameters.tileUV = new Vector2(animT > 0f ? (16f / world.Width) / animT : 0f,
                                                       animT > 0f ? (16f / world.Height) / animT : 0f);
        shader.Parameters.borderFade = 0.1f;
        shader.Parameters.area = 320;
        shader.Parameters.tileBorderWhite = tileBorderT;
        shader.Apply();

        sb.Draw(Assets.InvisPixel.Asset.Value, dest, new Rectangle(0, 0, 1, 1), teamColor * animT);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        string label = $"{_resizePreviewHW * 2 + 1} x {_resizePreviewHH * 2 + 1}" +
                       $"  (max {_resizeGloryCapW * 2 + 1} x {_resizeGloryCapH * 2 + 1})" +
                       "  [Click to confirm  |  RMB to cancel]";

        Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label);
        Vector2 labelPos = new Vector2((Main.screenWidth - labelSize.X) / 2f, Main.screenHeight - 80f);
        Utils.DrawBorderStringFourWay(sb, FontAssets.MouseText.Value, label, labelPos.X, labelPos.Y, teamColor, Color.Black, Vector2.Zero);
    }

    private Texture2D IconTexture(Option opt)
    {
        if (opt == Option.RemoveGem)
        {
            int baseFrameX = (Main.tile[TargetOrigin.X, TargetOrigin.Y].TileFrameX / 54) * 54;
            int itemID = baseFrameX switch
            {
                0 => ItemID.GemLockRuby,
                54 => ItemID.GemLockSapphire,
                108 => ItemID.GemLockEmerald,
                162 => ItemID.GemLockTopaz,
                216 => ItemID.GemLockAmethyst,
                324 => ItemID.GemLockAmber,
                _ => ItemID.GemLockAmethyst,
            };
            return TextureAssets.Item[itemID].Value;
        }

        if (opt == Option.Resize)
            return TextureAssets.Item[ItemID.MechanicalLens].Value;

        if (opt == Option.ToggleProtection)
        {
            var zones = ModContent.GetInstance<GemLockZones>();
            if (!zones.IsZoneEnabled(TargetOrigin))
                return TextureAssets.Item[ItemID.GoldenKey].Value;

            return TextureAssets.Item[zones.IsProtectionEnabled(TargetOrigin) ? ItemID.GoldenKey : ItemID.ChestLock].Value;
        }

        return TextureAssets.MagicPixel.Value;
    }

    public static void DoRemoveGem(Point origin)
    {
        int baseFrameX = (Main.tile[origin.X, origin.Y].TileFrameX / 54) * 54;

        int gemItem = baseFrameX switch
        {
            0 => ItemID.LargeRuby,
            54 => ItemID.LargeSapphire,
            108 => ItemID.LargeEmerald,
            162 => ItemID.LargeTopaz,
            216 => ItemID.LargeAmethyst,
            270 => ItemID.LargeDiamond,
            324 => ItemID.LargeAmber,
            _ => ItemID.LargeAmethyst,
        };

        Item.NewItem(new Terraria.DataStructures.EntitySource_TileInteraction(Main.LocalPlayer, origin.X, origin.Y), origin.X * 16, origin.Y * 16, 48, 48, gemItem);

        for (int tx = 0; tx < 3; tx++)
            for (int ty = 0; ty < 3; ty++)
            {
                var tile = Main.tile[origin.X + tx, origin.Y + ty];
                if (tile.HasTile && tile.TileFrameY >= 54)
                    tile.TileFrameY -= 54;
            }

        ModContent.GetInstance<GemLockZones>().RefreshLock(origin);
        NetMessage.SendTileSquare(-1, origin.X, origin.Y, 3, 3);
    }

    private static void SendRemoveGemRequest(Point origin)
    {
        var packet = ModContent.GetInstance<OxidizedMediumcore>().GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.RemoveGemRequest);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Send();
    }

    private void ExecuteOption(Option opt)
    {
        switch (opt)
        {
            case Option.RemoveGem:
                Active = false;
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    SendRemoveGemRequest(TargetOrigin);
                else
                    DoRemoveGem(TargetOrigin);
                break;

            case Option.Resize:
                var current = ModContent.GetInstance<GemLockZones>().GetZone(TargetOrigin);
                _resizePreviewHW = current.HalfWidth;
                _resizePreviewHH = current.HalfHeight;
                _resizeAnimTimer = 0;

                Team lockTeam = GemLockHelper.GetTeamForOrigin(TargetOrigin);
                if (lockTeam != Team.None)
                {
                    var glory = ModContent.GetInstance<TeamGlorySystem>();
                    _resizeGloryCapW = glory.GetMaxHalfWidth(lockTeam);
                    _resizeGloryCapH = glory.GetMaxHalfHeight(lockTeam);
                }
                else
                {
                    _resizeGloryCapW = GemLockZones.MaxHalf;
                    _resizeGloryCapH = GemLockZones.MaxHalf;
                }

                ResizeMode = true;
                break;

            case Option.ToggleProtection:
                Active = false;
                var zones = ModContent.GetInstance<GemLockZones>();
                if (!zones.IsZoneEnabled(TargetOrigin))
                {
                    if (zones.HasActiveZoneElsewhere(TargetOrigin)) break;
                    if (zones.HasOverlappingEnemyZone(TargetOrigin)) break;
                    zones.ToggleZone(TargetOrigin);
                }
                else
                    zones.ToggleProtection(TargetOrigin);

                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendTileSquare(-1, TargetOrigin.X, TargetOrigin.Y, 3, 3);
                break;
        }
    }
}