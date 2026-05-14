using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace OxidizedMediumcore.Core;

public sealed class OxidizedClientConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public bool AlwaysRenderGemlockZones = false;

    public float GemlockZonesIntensity = 1.0f;
}
