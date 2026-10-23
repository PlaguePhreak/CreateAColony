using CreateAColony.Colonies;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Blocks;

/// <summary>Tells a player trying to put a building block down by hand to use the Builder's Wand.</summary>
public static class WandOnly
{
    /// <summary>The server's PlaceHutsByHand setting as last sent to this client.</summary>
    public static bool ClientHandPlacing;

    public static void Tell(IWorldAccessor world, IPlayer player)
    {
        // Placing runs on both sides; the client says it once.
        if (world.Api is ICoreClientAPI capi) capi.TriggerIngameError(player, "createacolony-usewand", Lang.Get("createacolony:use-wand"));
    }

    /// <summary>Whether building blocks go down by hand (config PlaceHutsByHand), for making blueprints.</summary>
    public static bool HandPlacing(IWorldAccessor world)
    {
        if (world.Side == EnumAppSide.Client) return ClientHandPlacing;
        return world.Api.ModLoader.GetModSystem<ColonyManager>()?.Config.PlaceHutsByHand == true;
    }

    /// <summary>The blueprint rotation for a block placed by hand, facing the player as the wand would.</summary>
    public static int HandRotation(IPlayer player) => ColonyManager.RotationFor(player);
}
