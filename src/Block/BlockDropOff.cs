using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// Place next to a chest: couriers empty that chest into the warehouse. Players dump loot there and
/// let the colony sort it away; later, worker huts will use the same mechanism for their output.
/// </summary>
public class BlockDropOff : Block
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
        if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;

        colony.DropOffs.Add(blockSel.Position.Copy());
        manager.MarkDirty(colony);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side == EnumAppSide.Server && byPlayer is IServerPlayer player)
        {
            player.SendMessage(0, Lang.Get("createacolony:dropoff-help"), EnumChatType.Notification);
        }
        return true;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            var colony = manager.GetAt(pos);
            if (colony != null && colony.DropOffs.RemoveAll(p => p.Equals(pos)) > 0) manager.MarkDirty(colony);
        }
        base.OnBlockRemoved(world, pos);
    }
}
