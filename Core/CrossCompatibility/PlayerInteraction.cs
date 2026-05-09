using System;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace OxidizedMediumcore.Core.CrossCompatibility;

public sealed class PlayerInteraction : ModSystem
{
	private delegate void OrigDelayedSetTarget(object modInstance, int targetPlayerIndex);

	private Hook? _hook;

	public override void Load()
	{
		ModLoader.TryGetMod("Playerinteraction", out var mod);
		if (mod == null)
		{
			return;
		}

		Type? handlerType = mod.Code.GetTypes().FirstOrDefault((Type t) => t.Name == "InventoryNetHandler");
		if (handlerType is not null)
		{
			MethodInfo? method = handlerType.GetMethod("DelayedSetTarget", BindingFlags.Static | BindingFlags.NonPublic);
			if (method is not null)
			{
				_hook = new Hook(method, new Action<OrigDelayedSetTarget, object, int>(OnDelayedSetTarget));
			}
		}
	}

	public override void Unload()
	{
		Hook? hook = _hook;
        hook?.Dispose();
		_hook = null;
	}

	private static void OnDelayedSetTarget(OrigDelayedSetTarget orig, object modInstance, int targetPlayerIndex)
	{
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			orig(modInstance, targetPlayerIndex);
			return;
		}

		Player local = Main.LocalPlayer;
		Player target = Main.player[targetPlayerIndex];
		if (local.team != 0 && local.team == target.team)
		{
			orig(modInstance, targetPlayerIndex);
		}
	}
}