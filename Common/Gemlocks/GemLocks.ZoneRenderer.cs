using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OxidizedMediumcore.Common.Gemlocks;
using OxidizedMediumcore.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace OxidizedMediumcore.Common.Gemlocks;

public sealed class GemLockZoneRenderer : ModSystem
{
    private static readonly Rectangle PixelSource = new(0, 0, 1, 1);

    private static WrapperShaderData<Assets.GemZone.Parameters> Shader => Assets.GemZone.Createp0();

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int insertIndex = layers.FindIndex(l => l.Name == "Vanilla: Cursor");
        if (insertIndex < 0)
            insertIndex = layers.Count;

        layers.Insert(insertIndex, new LegacyGameInterfaceLayer("OxidizedMediumcore: GemLock Zones", DrawZones, InterfaceScaleType.Game));
    }

    private bool DrawZones()
    {
        if (Main.gameMenu || Main.netMode == Terraria.ID.NetmodeID.Server)
            return true;

        var sb = Main.spriteBatch;

        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        foreach (var origin in ModContent.GetInstance<GemLockZones>().ActiveZones)
            DrawZone(sb, origin);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);

        return true;
    }

    private static void DrawZone(SpriteBatch sb, Point origin)
    {
        if (!ModContent.GetInstance<GemLockZones>().IsProtectionEnabled(origin))
            return;

        Color color = GemLockHelper.TeamColor(GemLockHelper.GetTeamForOrigin(origin));
        var world = ModContent.GetInstance<GemLockZones>().GetZoneRect(origin);
        int timer = ModContent.GetInstance<GemLockZones>().GetActivationTimer(origin);
        const int animEnd = 15;

        float animT = Utils.GetLerpValue(0, animEnd, timer, clamped: true);
        animT = 1f - (1f - animT) * (1f - animT) * (1f - animT);

        int sw = (int)(world.Width * animT) / 16 * 16;
        int sh = (int)(world.Height * animT) / 16 * 16;

        var dest = new Rectangle(world.X + (world.Width - sw) / 2 - (int)Main.screenPosition.X, world.Y + (world.Height - sh) / 2 - (int)Main.screenPosition.Y, sw, sh);

        float disappearT = 1f - Utils.GetLerpValue(animEnd, animEnd + 30, timer, clamped: true);
        float tileBorderT = Utils.GetLerpValue(animEnd, animEnd + 15, timer, clamped: true);
        tileBorderT *= (1f - tileBorderT) * 4f;

        var shader = Shader;
        shader.Parameters.globalTime = (float)Main.timeForVisualEffects;
        shader.Parameters.teamColor = color.ToVector3();
        shader.Parameters.baseTexture = new HlslSampler2D { Texture = Assets.InvisPixel.Asset.Value, Sampler = SamplerState.LinearClamp };
        shader.Parameters.tileUV = new Vector2(animT > 0f ? (16f / world.Width) / animT : 0f, animT > 0f ? (16f / world.Height) / animT : 0f);
        shader.Parameters.borderFade = 0.1f;
        shader.Parameters.area = 320;
        shader.Parameters.tileBorderWhite = tileBorderT;
        shader.Apply();

        sb.Draw(Assets.InvisPixel.Asset.Value, dest, PixelSource, color * animT * disappearT);
    }
}