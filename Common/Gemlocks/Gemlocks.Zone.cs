using Microsoft.Xna.Framework;
using OxidizedMediumcore.Common.Glory;
using System.Collections.Generic;
using Terraria;
using Terraria.Enums;
using Terraria.ID;

namespace OxidizedMediumcore.Common.Gemlocks;

public struct GemLockZone(int hw, int hh)
{
    public static readonly GemLockZone Default = new(TeamGlorySystem.BaseHalfW, TeamGlorySystem.BaseHalfH);
    public int HalfWidth = hw;
    public int HalfHeight = hh;
    public int ActivationTimer = 0;
    public bool ProtectionEnabled = true;
    public bool ZoneEnabled = false;
}

public static class GemLockHelper
{
    private static Dictionary<int, Team> FrameTeam => new()
    {
        {   0, Team.Red    },
        {  54, Team.Blue   },
        { 108, Team.Green  },
        { 162, Team.Yellow },
        { 216, Team.Pink   },
    };

    public static Point GetOrigin(int i, int j)
    {
        var tile = Main.tile[i, j];
        return new Point(i - (tile.TileFrameX % 54) / 18, j - (tile.TileFrameY % 54) / 18);
    }

    public static Team GetTeamForOrigin(Point origin)
    {
        int baseFrameX = (Main.tile[origin.X, origin.Y].TileFrameX / 54) * 54;
        return FrameTeam.GetValueOrDefault(baseFrameX, Team.None);
    }

    public static Team GetRequiredTeam(int i, int j, int type) => type == TileID.GemLocks ? GetTeamForOrigin(GetOrigin(i, j)) : Team.None;

    public static bool HasGem(Point origin)
    {
        var tile = Main.tile[origin.X, origin.Y];
        return tile.type == TileID.GemLocks && (tile.TileFrameY / 54) * 54 >= 54;
    }

    public static string GetTeamName(Team team) => team switch
    {
        Team.Red => "Red",
        Team.Blue => "Blue",
        Team.Green => "Green",
        Team.Yellow => "Yellow",
        Team.Pink => "Pink",
        _ => "None",
    };
}