using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OxidizedMediumcore.Common.Gemlocks;
using OxidizedMediumcore.Common.Glory;
using System.Linq;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Common.Admin;

// this ui is sooo bad :sob:
public static class AdminPanelUI
{
    public static int PanelW => 320;
    public static int PanelPad => 18;
    public static int RowH => 16;
    public static float TitleScale => 0.85f;
    public static float RowScale => 0.70f;
    private static int PlayerAreaMaxH => 110;
    private static int HeaderH => 178;
    private static int GloryHeaderH => 164;
    private static int _totalPlayerCount = 0;
    private static int PlayerContentH => _totalPlayerCount * 22;
    private static int PlayerAreaVisibleH => System.Math.Min(PlayerAreaMaxH, PlayerContentH);
    public static int PanelH => HeaderH + GloryHeaderH + PlayerAreaVisibleH + PanelPad;
    private static float _scrollOffset = 0f;
    private static int _prevScrollValue = int.MinValue;
    private static bool _prevLeftDown = false;
    private static int _holdTick = 0;
    private const int HoldDelay = 20;
    private const int HoldInterval = 4;

    public static void Draw(SpriteBatch sb, int anchorX, int anchorY)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        if (!Main.LocalPlayer.GetModPlayer<AdminPlayer>().IsAdmin)
            return;

        _totalPlayerCount = Main.player.Count(p => p.active);

        int curScroll = Mouse.GetState().ScrollWheelValue;
        if (_prevScrollValue == int.MinValue)
        {
            _prevScrollValue = curScroll;
        }
        else
        {
            var panelRect = new Rectangle(anchorX, anchorY, PanelW, PanelH);
            if (panelRect.Contains(Main.mouseX, Main.mouseY))
            {
                int scrollDelta = curScroll - _prevScrollValue;
                _scrollOffset -= scrollDelta * 0.04f;
            }
            _prevScrollValue = curScroll;
        }

        float maxScroll = System.Math.Max(0f, PlayerContentH - PlayerAreaVisibleH);
        _scrollOffset = System.Math.Clamp(_scrollOffset, 0f, maxScroll);

        DrawPanel(sb, anchorX, anchorY, PanelW, PanelH);

        float contentX = anchorX + PanelPad;
        float contentY = anchorY + PanelPad;

        Utils.DrawBorderString(sb, "Admin Panel", new Vector2(contentX, contentY), Color.White, TitleScale);
        contentY += 20f;

        DrawHLine(sb, anchorX + 2, (int)contentY, PanelW - 4);
        contentY += 8f;

        bool clicked = IsLeftClick();
        bool held = Mouse.GetState().LeftButton == ButtonState.Pressed;

        DrawRow(sb, label: "Giant gems craft", value: AdminConfig.DisableGemCraftRecipe, rowX: anchorX, contentX: contentX, ref contentY, clicked: clicked, onToggle: () =>
        {
            AdminConfig.DisableGemCraftRecipe = !AdminConfig.DisableGemCraftRecipe;
            if (Main.netMode == NetmodeID.Server)
                AdminConfig.BroadcastSync();
            else
                AdminConfig.SendChange();
            string state = AdminConfig.DisableGemCraftRecipe ? "allowed to be crafted" : "blocked from being crafted";
            Main.NewText($"Giant gems are now {state}", Color.White.R, Color.White.G, Color.White.B);
        });

        contentY += 4f;

        string modeStr = AdminConfig.ProtectionMode switch
        {
            TileProtectionMode.None => "None",
            TileProtectionMode.Legacy => "Legacy",
            TileProtectionMode.Restrictive => "Restrictive",
            _ => "Unknown"
        };
        Color modeCol = AdminConfig.ProtectionMode == TileProtectionMode.None ? Color.IndianRed : (AdminConfig.ProtectionMode == TileProtectionMode.Legacy ? Color.Gold : Color.LightGreen);

        DrawModeRow(sb, label: "Tile protection", valueStr: modeStr, valueCol: modeCol, rowX: anchorX, contentX: contentX, ref contentY, clicked: clicked, onToggle: () =>
        {
            AdminConfig.ProtectionMode = (TileProtectionMode)(((int)AdminConfig.ProtectionMode + 1) % 3);
            if (Main.netMode == NetmodeID.Server)
                AdminConfig.BroadcastSync();
            else
                AdminConfig.SendChange();
            Main.NewText($"Tile protection is now {AdminConfig.ProtectionMode}", Color.White.R, Color.White.G, Color.White.B);
        });

        contentY += 4f;

        DrawFloatRow(sb, label: "Blast radius mult", ref AdminConfig.BlastRadiusMultiplierRef, min: 0.0f, max: 1.0f, step: 0.05f, format: "0.00", rowX: anchorX, contentX: contentX, ref contentY, clicked: clicked, held: held, onChanged: () =>
        {
            if (Main.netMode == NetmodeID.Server)
                AdminConfig.BroadcastSync();
            else
                AdminConfig.SendChange();
        });

        contentY += 4f;

        DrawFloatRow(sb, label: "Mining fatigue", ref AdminConfig.PickSpeedPenaltyRef, min: 0f, max: 1.0f, step: 0.005f, format: "0.00", rowX: anchorX, contentX: contentX, ref contentY, clicked: clicked, held: held, onChanged: () =>
        {
            if (Main.netMode == NetmodeID.Server)
                AdminConfig.BroadcastSync();
            else
                AdminConfig.SendChange();
        });

        contentY += 6f;
        DrawHLine(sb, anchorX + 2, (int)contentY, PanelW - 4);
        contentY += 6f;

        DrawGlorySection(sb, anchorX, contentX, ref contentY, clicked, held);

        contentY += 6f;
        DrawHLine(sb, anchorX + 2, (int)contentY, PanelW - 4);
        contentY += 6f;

        Utils.DrawBorderString(sb, "Players:", new Vector2(contentX, contentY), Color.White, TitleScale);
        contentY += 18f;

        int playerAreaTop = (int)contentY;
        int playerAreaLeft = anchorX + 2;
        int playerAreaW = PanelW - 4;

        var prevRasterizer = sb.GraphicsDevice.RasterizerState;
        var prevScissor = sb.GraphicsDevice.ScissorRectangle;

        var uiMatrix = Main.UIScaleMatrix;
        Vector2 tlScreen = Vector2.Transform(new Vector2(playerAreaLeft, playerAreaTop), uiMatrix);
        Vector2 brScreen = Vector2.Transform(new Vector2(playerAreaLeft + playerAreaW, playerAreaTop + PlayerAreaVisibleH), uiMatrix);
        int bbW = sb.GraphicsDevice.PresentationParameters.BackBufferWidth;
        int bbH = sb.GraphicsDevice.PresentationParameters.BackBufferHeight;
        int sx = (int)System.Math.Clamp(tlScreen.X, 0, bbW - 1);
        int sy = (int)System.Math.Clamp(tlScreen.Y, 0, bbH - 1);
        int sw = (int)System.Math.Clamp(brScreen.X - tlScreen.X, 1, bbW - sx);
        int sh = (int)System.Math.Clamp(brScreen.Y - tlScreen.Y, 1, bbH - sy);
        var scissorRect = new Rectangle(sx, sy, sw, sh);

        sb.End();
        var scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, scissorRasterizer, null, Main.UIScaleMatrix);
        sb.GraphicsDevice.ScissorRectangle = scissorRect;

        float playerY = contentY - _scrollOffset;
        foreach (var p in Main.player.Where(p => p.active))
            DrawPlayerRow(sb, p, anchorX, contentX, ref playerY, clicked);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, prevRasterizer ?? RasterizerState.CullNone, null, Main.UIScaleMatrix);
        sb.GraphicsDevice.ScissorRectangle = prevScissor;

        if (PlayerContentH > PlayerAreaVisibleH)
            DrawScrollBar(sb, anchorX, playerAreaTop, PlayerAreaVisibleH);

        if (held)
            _holdTick++;
        else
            _holdTick = 0;

        _prevLeftDown = held;
    }

    private static bool IsLeftClick()
    {
        bool curDown = Mouse.GetState().LeftButton == ButtonState.Pressed;
        return curDown && !_prevLeftDown;
    }

    private static bool ShouldFire(bool held, bool clicked)
    {
        if (clicked)
            return true;

        if (held && _holdTick > HoldDelay && (_holdTick - HoldDelay) % HoldInterval == 0)
            return true;

        return false;
    }

    private static void DrawFloatRow(SpriteBatch sb, string label, ref float value, float min, float max, float step, string format, int rowX, float contentX, ref float y, bool clicked, bool held, System.Action onChanged)
    {
        const int BtnW = 14;
        const int BtnH = 14;
        const int ValueW = 38;

        float rowY = y;

        Utils.DrawBorderString(sb, label + ":", new Vector2(contentX, rowY), Color.LightSteelBlue, RowScale);

        float rightEdge = contentX + (PanelW - PanelPad * 2);
        float valueRight = rightEdge;
        float plusLeft = valueRight - BtnW;
        float valueLeft = plusLeft - ValueW - 2;
        float minusLeft = valueLeft - BtnW - 2;

        var minusRect = new Rectangle((int)minusLeft, (int)rowY, BtnW, BtnH);
        bool minusHover = minusRect.Contains(Main.mouseX, Main.mouseY);
        sb.Draw(TextureAssets.MagicPixel.Value, minusRect, minusHover ? new Color(80, 80, 120) * 0.9f : new Color(50, 50, 80) * 0.8f);
        Utils.DrawBorderString(sb, "-", new Vector2(minusLeft + 3f, rowY - 1f), Color.White, RowScale);

        if (minusHover && ShouldFire(held, clicked))
        {
            value = System.Math.Clamp(value - step, min, max);
            value = (float)System.Math.Round(value / step) * step;
            onChanged();
        }

        string valStr = value.ToString(format);
        float vw = FontAssets.ItemStack.Value.MeasureString(valStr).X * RowScale;
        float valCenterX = valueLeft + (ValueW - vw) * 0.5f;
        Utils.DrawBorderString(sb, valStr, new Vector2(valCenterX, rowY), Color.Gold, RowScale);

        var plusRect = new Rectangle((int)plusLeft, (int)rowY, BtnW, BtnH);
        bool plusHover = plusRect.Contains(Main.mouseX, Main.mouseY);
        sb.Draw(TextureAssets.MagicPixel.Value, plusRect, plusHover ? new Color(80, 80, 120) * 0.9f : new Color(50, 50, 80) * 0.8f);
        Utils.DrawBorderString(sb, "+", new Vector2(plusLeft + 2f, rowY - 1f), Color.White, RowScale);

        if (plusHover && ShouldFire(held, clicked))
        {
            value = System.Math.Clamp(value + step, min, max);
            value = (float)System.Math.Round(value / step) * step;
            onChanged();
        }

        y += RowH + 4f;
    }

    private static void DrawScrollBar(SpriteBatch sb, int anchorX, int areaTop, int areaH)
    {
        const int BarW = 4;
        int barX = anchorX + PanelW - BarW - 2;

        sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(barX, areaTop, BarW, areaH), Color.Black * 0.4f);

        float ratio = (float)PlayerAreaVisibleH / PlayerContentH;
        int thumbH = System.Math.Max(12, (int)(areaH * ratio));
        float maxScroll = PlayerContentH - PlayerAreaVisibleH;
        float scrollFraction = maxScroll > 0 ? _scrollOffset / maxScroll : 0f;
        int thumbY = areaTop + (int)((areaH - thumbH) * scrollFraction);

        sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(barX, thumbY, BarW, thumbH), Color.LightGray * 0.6f);
    }

    private static void DrawPanel(SpriteBatch sb, int x, int y, int w, int h)
    {
        var tex2 = Main.Assets.Request<Texture2D>("Images/UI/PanelBorder").Value;
        var p0 = Assets.Gradient.Createp0();

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        p0.Parameters.direction = Vector2.UnitX * -1;
        p0.Parameters.gradientSharpness = 1f;
        p0.Parameters.cornerRadius = 0.035f;
        p0.Apply();

        sb.Draw(Assets.InvisPixel.Asset.Value, new Rectangle(x, y, w, h), Color.Black * 0.8f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        Utils.DrawSplicedPanel(sb, tex2, x, y, w, h, 12, 12, 12, 12, Color.White * 0.85f);
    }

    private static void DrawHLine(SpriteBatch sb, int x, int y, int w) => sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, w, 2), Color.White * 0.85f);

    private static void DrawRow(SpriteBatch sb, string label, bool value, int rowX, float contentX, ref float y, bool clicked, System.Action onToggle)
    {
        var rowRect = new Rectangle(rowX + 2, (int)y, PanelW - 4, RowH);
        bool hovered = rowRect.Contains(Main.mouseX, Main.mouseY);

        if (hovered)
            sb.Draw(TextureAssets.MagicPixel.Value, rowRect, Color.White * 0.1f);

        Utils.DrawBorderString(sb, label + ":", new Vector2(contentX, y), Color.LightSteelBlue, RowScale);

        string valueStr = value ? "Allowed" : "Blocked";
        Color valueCol = value ? Color.LightGreen : Color.IndianRed;
        float vw = FontAssets.ItemStack.Value.MeasureString(valueStr).X * RowScale;
        float rightX = contentX + (PanelW - PanelPad * 2.25f) - vw;

        Utils.DrawBorderString(sb, valueStr, new Vector2(rightX, y), valueCol, RowScale);

        if (hovered && clicked)
            onToggle();

        y += RowH;
    }

    private static void DrawModeRow(SpriteBatch sb, string label, string valueStr, Color valueCol, int rowX, float contentX, ref float y, bool clicked, System.Action onToggle)
    {
        var rowRect = new Rectangle(rowX + 2, (int)y, PanelW - 4, RowH);
        bool hovered = rowRect.Contains(Main.mouseX, Main.mouseY);

        if (hovered)
            sb.Draw(TextureAssets.MagicPixel.Value, rowRect, Color.White * 0.1f);

        Utils.DrawBorderString(sb, label + ":", new Vector2(contentX, y), Color.LightSteelBlue, RowScale);

        float vw = FontAssets.ItemStack.Value.MeasureString(valueStr).X * RowScale;
        float rightX = contentX + (PanelW - PanelPad * 2.25f) - vw;

        Utils.DrawBorderString(sb, valueStr, new Vector2(rightX, y), valueCol, RowScale);

        if (hovered && clicked)
            onToggle();

        y += RowH;
    }

    private static Color TeamColor(int team) => team switch
    {
        1 => new Color(255, 80, 80),
        2 => new Color(80, 255, 80),
        3 => new Color(80, 150, 255),
        4 => new Color(255, 230, 50),
        5 => new Color(255, 150, 200),
        _ => Color.LightGray,
    };


    private const int GloryStep = 5;

    private static void DrawGlorySection(SpriteBatch sb, int anchorX, float contentX, ref float y, bool clicked, bool held)
    {
        Utils.DrawBorderString(sb, "Party Glory:", new Vector2(contentX, y), Color.White, TitleScale);
        y += 20f;

        var glory = ModContent.GetInstance<TeamGlorySystem>();

        Team[] teams = { Team.Red, Team.Blue, Team.Green, Team.Yellow, Team.Pink };
        foreach (var team in teams)
            DrawGloryTeamRow(sb, team, glory, anchorX, contentX, ref y, clicked, held);
    }

    private static void DrawGloryTeamRow(SpriteBatch sb, Team team, TeamGlorySystem glory, int anchorX, float contentX, ref float y, bool clicked, bool held)
    {
        const int BtnW = 14;
        const int BtnH = 14;
        const int BarH = 6;
        const int ValueW = 34;
        const int MaxBarW = 80;

        Color teamCol = GemLockHelper.TeamColor(team);
        string teamName = GemLockHelper.GetTeamName(team);

        int curGlory = glory.GetGlory(team);
        int maxHW = glory.GetMaxHalfWidth(team);
        int maxHH = glory.GetMaxHalfHeight(team);

        int baseHW = TeamGlorySystem.BaseHalfW;
        int hardHW = TeamGlorySystem.HardCapHalfW;
        float capFraction = hardHW > baseHW
            ? System.Math.Clamp((float)(maxHW - baseHW) / (hardHW - baseHW), 0f, 1f)
            : 1f;

        float rightEdge = contentX + (PanelW - PanelPad * 2);

        float plusLeft = rightEdge - BtnW;
        float valueMid = plusLeft - ValueW - 2;
        float minusLeft = valueMid - BtnW - 2;

        var minusRect = new Rectangle((int)minusLeft, (int)y, BtnW, BtnH);
        bool minusHover = minusRect.Contains(Main.mouseX, Main.mouseY);
        sb.Draw(TextureAssets.MagicPixel.Value, minusRect, minusHover ? new Color(80, 80, 120) * 0.9f : new Color(50, 50, 80) * 0.8f);
        Utils.DrawBorderString(sb, "-", new Vector2(minusLeft + 3f, y - 1f), Color.White, RowScale);

        if (minusHover && ShouldFire(held, clicked))
        {
            int step = Main.keyState.IsKeyDown(Keys.LeftShift) ? GloryStep * 10 : GloryStep;
            glory.AddGlory(team, -step, broadcast: true);
            SyncGloryChange(team);
        }

        string valStr = curGlory.ToString();
        float vw = FontAssets.ItemStack.Value.MeasureString(valStr).X * RowScale;
        float valX = valueMid + (ValueW - vw) * 0.5f;
        Utils.DrawBorderString(sb, valStr, new Vector2(valX, y), Color.Gold, RowScale);

        var plusRect = new Rectangle((int)plusLeft, (int)y, BtnW, BtnH);
        bool plusHover = plusRect.Contains(Main.mouseX, Main.mouseY);
        sb.Draw(TextureAssets.MagicPixel.Value, plusRect, plusHover ? new Color(80, 80, 120) * 0.9f : new Color(50, 50, 80) * 0.8f);
        Utils.DrawBorderString(sb, "+", new Vector2(plusLeft + 2f, y - 1f), Color.White, RowScale);

        if (plusHover && ShouldFire(held, clicked))
        {
            int step = Main.keyState.IsKeyDown(Keys.LeftShift) ? GloryStep * 10 : GloryStep;
            glory.AddGlory(team, +step, broadcast: true);
            SyncGloryChange(team);
        }

        Utils.DrawBorderString(sb, teamName, new Vector2(contentX, y), teamCol, RowScale);

        string capStr = $"{maxHW * 2 + 1}x{maxHH * 2 + 1}";
        float capW = FontAssets.ItemStack.Value.MeasureString(capStr).X * RowScale;
        float capX = minusLeft - capW - 6f;
        Utils.DrawBorderString(sb, capStr, new Vector2(capX, y), new Color(160, 200, 160), RowScale);

        float labelW = FontAssets.ItemStack.Value.MeasureString(teamName).X * RowScale;
        float barX = contentX + labelW + 6f;
        float availableBarW = capX - barX - 6f;
        int barW = (int)System.Math.Min(MaxBarW, availableBarW);
        if (barW > 10)
        {
            int barTop = (int)y + BtnH - BarH - 1;
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)barX, barTop, barW, BarH), new Color(40, 40, 60, 180));
            int fillW = System.Math.Max(1, (int)(barW * capFraction));
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)barX, barTop, fillW, BarH), teamCol * 0.85f);
        }

        y += BtnH + 4f;
    }

    private static void SyncGloryChange(Team team)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            var mod = ModContent.GetInstance<OxidizedMediumcore>();
            var packet = mod.GetPacket();
            packet.Write((byte)OxidizedMediumcore.PacketID.GlorySync);
            packet.Write((byte)team);
            packet.Write(ModContent.GetInstance<TeamGlorySystem>().GetGlory(team));
            packet.Send();
        }
    }

    private static void DrawPlayerRow(SpriteBatch sb, Player p, int rowX, float contentX, ref float y, bool clicked)
    {
        var rowRect = new Rectangle(rowX + 2, (int)y, PanelW - 4, 20);
        bool hovered = rowRect.Contains(Main.mouseX, Main.mouseY);

        if (hovered)
            sb.Draw(TextureAssets.MagicPixel.Value, rowRect, Color.White * 0.1f);

        Utils.DrawBorderString(sb, p.name, new Vector2(contentX, y + 2f), TeamColor(p.team), RowScale);

        var shieldTex = TextureAssets.Pvp[1].Value;
        Rectangle frameBase = TextureAssets.Pvp[1].Frame(6);
        int shieldW = frameBase.Width - 2;
        int shieldH = frameBase.Height;
        float shieldsStartX = rowX + PanelW - PanelPad - (shieldW + 1) * 6;

        for (int i = 0; i < 6; i++)
        {
            var src = new Rectangle(frameBase.Width * i, 0, shieldW, shieldH);
            var dest = new Vector2(shieldsStartX + i * (shieldW + 1), y + 1f);
            float alpha = p.team == i ? 1f : 0.35f;
            sb.Draw(shieldTex, dest, src, Color.White * alpha);

            if (!clicked)
                continue;

            var clickRect = new Rectangle((int)dest.X, (int)dest.Y, shieldW, shieldH);
            if (!clickRect.Contains(Main.mouseX, Main.mouseY))
                continue;

            p.team = i;
            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, p.whoAmI);
        }

        y += 22f;
    }
}