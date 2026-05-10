using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OxidizedMediumcore.Common.Gemlocks;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace OxidizedMediumcore.Common.BossOwnership;

public static class BossOwnershipSync
{
    private const double BroadcastCooldownSeconds = 2.0;
    private static double _lastBroadcastTime = -999.0;

    public static void BroadcastOwnership(NPC npc, Team newOwner, bool isSteal)
    {
        if (Main.netMode != NetmodeID.Server && Main.netMode != NetmodeID.SinglePlayer)
            return;

        double now = Main.gameTimeCache.TotalGameTime.TotalSeconds;
        if (now - _lastBroadcastTime < BroadcastCooldownSeconds)
            return;

        _lastBroadcastTime = now;

        string teamName = GemLockHelper.GetTeamName(newOwner);
        Color teamColor = BossOwnershipSystem.TeamColor(newOwner);
        string msg = isSteal
            ? $"{teamName} party stole {npc.GivenOrTypeName}!"
            : $"{teamName} party is now fighting {npc.GivenOrTypeName}!";

        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(msg), teamColor);

        if (Main.netMode == NetmodeID.Server)
            SendSync(npc.whoAmI, newOwner, toClient: -1, ignoreClient: -1);
    }

    public static void SendSync(int npcWhoAmI, Team owner, int toClient, int ignoreClient)
    {
        var packet = ModContent.GetInstance<OxidizedMediumcore>().GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.BossOwnerSync);
        packet.Write((short)npcWhoAmI);
        packet.Write((byte)owner);
        packet.Send(toClient, ignoreClient);
    }

    public static void HandleSync(BinaryReader reader)
    {
        int npcWhoAmI = reader.ReadInt16();
        Team owner = (Team)reader.ReadByte();
        ModContent.GetInstance<BossOwnershipClient>().SetOwner(npcWhoAmI, owner);
    }
}

public sealed class BossOwnershipClient : ModSystem
{
    private readonly Dictionary<int, Team> _ownerMap = new();

    public void SetOwner(int npcWhoAmI, Team owner)
    {
        if (owner == Team.None)
            _ownerMap.Remove(npcWhoAmI);
        else
            _ownerMap[npcWhoAmI] = owner;
    }

    public Team GetOwner(int npcWhoAmI) => _ownerMap.TryGetValue(npcWhoAmI, out var t) ? t : Team.None;

    public override void PostUpdateEverything()
    {
        var toRemove = new List<int>();

        foreach (int key in _ownerMap.Keys)
        {
            if (key < 0 || key >= Main.maxNPCs || !Main.npc[key].active)
                toRemove.Add(key);
        }

        foreach (int key in toRemove)
            _ownerMap.Remove(key);
    }

    public override void OnWorldUnload() => _ownerMap.Clear();

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int idx = layers.FindIndex(l => l.Name == "Vanilla: Cursor");
        
        if (idx < 0) 
            idx = layers.Count;

        layers.Insert(idx, new LegacyGameInterfaceLayer("OxidizedMediumcore: Boss Ownership Tags", DrawOwnershipTags, InterfaceScaleType.Game));
    }

    private bool DrawOwnershipTags()
    {
        if (Main.gameMenu || Main.netMode == NetmodeID.Server)
            return true;

        var sb = Main.spriteBatch;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            SamplerState.LinearClamp, DepthStencilState.None,
            RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        var drawnTeams = new HashSet<Team>();

        foreach (var (whoAmI, team) in _ownerMap)
        {
            NPC npc = Main.npc[whoAmI];
            if (!npc.active || BossOwnershipSystem.IsWormSegment(npc.type))
                continue;
            if (!drawnTeams.Add(team))
                continue;

            DrawTag(sb, npc, team);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            Main.DefaultSamplerState, DepthStencilState.None,
            Main.Rasterizer, null,
            Main.UIScaleMatrix);

        return true;
    }

    private static void DrawTag(SpriteBatch sb, NPC npc, Team team)
    {
        Vector2 worldPos = new(npc.position.X + npc.width * 0.5f, npc.position.Y - 28f);
        Vector2 screenPos = worldPos - Main.screenPosition;

        Color teamColor = BossOwnershipSystem.TeamColor(team);
        string label = GemLockHelper.GetTeamName(team);

        const float textScale = 0.5f;
        const float padX = 5f;
        const float padY = 2f;

        var font = FontAssets.MouseText.Value;
        Vector2 textSize = font.MeasureString(label) * textScale;
        float pillW = textSize.X + padX * 2f;
        float pillH = textSize.Y + padY * 2f;

        var pillRect = new Rectangle(
            (int)(screenPos.X - pillW * 0.5f),
            (int)(screenPos.Y - pillH),
            (int)pillW,
            (int)pillH);

        sb.Draw(TextureAssets.MagicPixel.Value, pillRect, new Color(0, 0, 0, 160));
        DrawBorder(sb, pillRect, teamColor * 0.85f);

        Vector2 textPos = new(screenPos.X - textSize.X * 0.5f, screenPos.Y - pillH + padY);
        Utils.DrawBorderString(sb, label, textPos, teamColor, textScale);
    }

    private static void DrawBorder(SpriteBatch sb, Rectangle r, Color c)
    {
        var px = TextureAssets.MagicPixel.Value;
        sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}

public sealed class BossOwnershipSyncOnJoin : ModPlayer
{
    public override void OnEnterWorld()
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        var sys = ModContent.GetInstance<BossOwnershipSystem>();

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.boss && !BossOwnershipSystem.IsBossSegment(npc.type))
                continue;

            Team owner = sys.GetOwner(npc.whoAmI);

            if (owner != Team.None)
                BossOwnershipSync.SendSync(npc.whoAmI, owner, toClient: Player.whoAmI, ignoreClient: -1);
        }
    }
}