using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OxidizedMediumcore.Common.Admin;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Gamepad;

namespace OxidizedMediumcore.Common.TeamControl;

public sealed class TeamControlSystem : ModSystem
{
    public override void Load() => On_Main.DrawPVPIcons += OnDrawPVPIcons;

    private static void OnDrawPVPIcons(On_Main.orig_DrawPVPIcons orig)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            orig();
            return;
        }

        var localHasSelected = Main.LocalPlayer.GetModPlayer<HasSelectedTeam>();
        bool locked = localHasSelected.HasSelected;

        if (Main.EquipPage == 1)
        {
            if (Main.hidePVPIcons)
                return;
        }
        else
            Main.hidePVPIcons = false;

        Main.inventoryScale = 0.6f;
        int num = (int)(52f * Main.inventoryScale);
        int num2 = 707 - num * 4 + Main.screenWidth - 800;
        int num3 = 114 + Main.mH + num * 2 + num / 2 - 12;
        int pvpIconY = num3 - 8;

        if (Main.EquipPage == 2) num2 += num + num / 2;
        {
            if (Main.mouseX > num2 - 7 && Main.mouseX < num2 + 25
             && Main.mouseY > pvpIconY - 2 && Main.mouseY < pvpIconY + 37
             && !PlayerInput.IgnoreMouseInterface)
                Main.LocalPlayer.mouseInterface = true;

            Rectangle rect = TextureAssets.Pvp[0].Frame(4, 6);
            rect.Location = new Point(rect.Width * 2, rect.Height * Main.LocalPlayer.team);
            rect.Width--;
            rect.Height--;

            Main.spriteBatch.Draw(TextureAssets.Pvp[0].Value, new Vector2(num2 - 10, pvpIconY), rect, Color.White * 0.35f, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
            UILinkPointNavigator.SetPosition(1550, new Vector2(num2 - 10, pvpIconY) + rect.Size() * 0.75f);

            float pvpTextScale = 0.65f;
            string[] pvpLines = { "Combat is", "always on" };
            float pvpTextY = pvpIconY + rect.Height + 4;
            foreach (string line in pvpLines)
            {
                float lw = FontAssets.ItemStack.Value.MeasureString(line).X * pvpTextScale;
                Utils.DrawBorderString(Main.spriteBatch, line, new Vector2(num2 - 20 + (rect.Width - lw) * 0.5f, pvpTextY), new Color(180, 180, 180), pvpTextScale);
                pvpTextY += FontAssets.ItemStack.Value.MeasureString("A").Y * pvpTextScale;
            }
        }

        num3 += 65;
        num2 -= 10;

        Rectangle rect2 = TextureAssets.Pvp[1].Frame(6);
        int shieldsL = num2;
        int shieldsT = num3;

        for (int i = 0; i < 6; i++)
        {
            Rectangle r = new(num2 + i % 2 * 20, num3 + i / 2 * 20, rect2.Width, rect2.Height);
            bool hovered = r.Contains(Main.MouseScreen.ToPoint()) && !PlayerInput.IgnoreMouseInterface;

            if (hovered)
                Main.LocalPlayer.mouseInterface = true;

            Rectangle src = rect2;
            src.X = rect2.Width * i;
            src.Width -= 2;

            if (!locked)
            {
                bool isCurrentTeam = Main.LocalPlayer.team == i;
                Color btnColor = isCurrentTeam
                    ? Color.White
                    : (hovered ? Color.White * 0.75f : Color.White * 0.45f);

                Main.spriteBatch.Draw(TextureAssets.Pvp[1].Value, r.Location.ToVector2(), src, btnColor);

                if (hovered && Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.LocalPlayer.team = i;

                    if (Main.netMode != NetmodeID.SinglePlayer)
                    {
                        NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, Main.myPlayer);
                    }

                    localHasSelected.ConfirmTeam();
                }
            }
            else
            {
                Main.spriteBatch.Draw(TextureAssets.Pvp[1].Value, r.Location.ToVector2(), src, Color.White * 0.35f);
            }

            UILinkPointNavigator.SetPosition(1551 + i, r.Location.ToVector2() + r.Size() * 0.75f);
        }

        float textScale = 0.65f;
        string[] lines = locked ? new[] { "Party selection", "is locked" } : new[] { "Choose your", "party!" };
        float rightEdge = shieldsL + 2 * 15f;
        float lineHeight = FontAssets.ItemStack.Value.MeasureString("A").Y * textScale;
        float textY = shieldsT + 3 * 20f;

        Color textColor = locked ? new Color(180, 180, 180) : new Color(255, 230, 100);
        foreach (string line in lines)
        {
            float lw = FontAssets.ItemStack.Value.MeasureString(line).X * textScale;
            Utils.DrawBorderString(Main.spriteBatch, line, new Vector2(rightEdge - lw, textY), textColor, textScale);
            textY += lineHeight;
        }

        AdminPanelUI.Draw(Main.spriteBatch, (int)MathF.Round(shieldsL - 14 * 20), (int)textY + 6);
    }
}

//public sealed class TeamUICommand : ModCommand
//{
//    private static bool _active = false;
//    public override string Command => "teamui";
//    public override CommandType Type => CommandType.Chat;
//    public override string Usage => "/teamui";
//    public override string Description => "Toggles the multiplayer party/PvP UI in singleplayer.";

//    public override void Load() => On_Main.DrawInventory += OnDrawInventory;
//    public override void Unload()
//    {
//        On_Main.DrawInventory -= OnDrawInventory;
//        _active = false;
//    }

//    private static void OnDrawInventory(On_Main.orig_DrawInventory orig, Main self)
//    {
//        if (!_active) { orig(self); return; }
//        int saved = Main.netMode;
//        Main.netMode = NetmodeID.MultiplayerClient;
//        try { orig(self); }
//        finally { Main.netMode = saved; }
//    }

//    public override void Action(CommandCaller caller, string input, string[] args)
//    {
//        caller.Reply("Mod features are disabled in singleplayer.", Color.Red);
//        return;
//    }
//}

public sealed class ForcePvPPlayer : ModPlayer
{
    public override void PreUpdate()
    {
        if (AdminConfig.ForcePvP && Main.netMode != NetmodeID.SinglePlayer)
        {
            Player.hostile = true;
        }
    }
}