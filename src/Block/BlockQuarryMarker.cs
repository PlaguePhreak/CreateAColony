using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// Marks the middle of a Quarry's pit. Put it on the ground near a Quarry; its quarrier digs out the
/// square pit around it, starting by picking up the marker itself. Placing a new marker moves the pit.
/// </summary>
public class BlockQuarryMarker : Block
{
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player)
        {
            return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
        }

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        if (colony == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member)
        {
            player.SendIngameError("createacolony-outside", Lang.Get("createacolony:marker-outside"));
            return false;
        }
        var quarry = manager.QuarryFor(colony, blockSel.Position);
        if (quarry == null)
        {
            player.SendIngameError("createacolony-noquarry", Lang.Get("createacolony:quarry-marker-no-hut", manager.Config.QuarryMarkerRange));
            return false;
        }
        if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;

        manager.SetQuarry(colony, quarry, blockSel.Position);
        int size = (System.Math.Max(3, manager.Config.QuarrySize) - 1) / 2 * 2 + 1;
        player.SendMessage(0, Lang.Get("createacolony:quarry-marker-placed", ColonyManager.BuildingName(quarry), size), EnumChatType.Notification);
        return true;
    }
}
