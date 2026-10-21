using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Blocks;

/// <summary>Tells a player trying to put a building block down by hand to use the Builder's Wand.</summary>
public static class WandOnly
{
    public static void Tell(IWorldAccessor world, IPlayer player)
    {
        // Placing runs on both sides; the client says it once.
        if (world.Api is ICoreClientAPI capi) capi.TriggerIngameError(player, "createacolony-usewand", Lang.Get("createacolony:use-wand"));
    }
}
