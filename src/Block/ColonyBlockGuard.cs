using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>Lets the server decide whether a Town Hall or hut block may be broken (see ColonyManager.BreakRefusal).</summary>
public static class ColonyBlockGuard
{
    /// <summary>
    /// True when the block may break now. The client never breaks it by itself: it waits for the server, which
    /// either breaks it (and tells every client) or keeps it and says why.
    /// </summary>
    public static bool MayBreak(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, bool townHall)
    {
        if (world.Side == EnumAppSide.Client) return false;
        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        if (manager?.BreakRefusal(byPlayer, pos, townHall) is not { } why) return true;
        if (byPlayer is IServerPlayer player) player.SendIngameError("createacolony-break", Lang.GetL(player.LanguageCode, why));
        world.BlockAccessor.MarkBlockDirty(pos);
        return false;
    }
}
