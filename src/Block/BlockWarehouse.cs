using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// Marks the colony's warehouse. Every chest within a few blocks of it becomes warehouse storage,
/// which couriers fill from drop-offs and draw from for delivery requests. One per colony.
/// </summary>
public class BlockWarehouse : Block
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
        if (colony.WarehousePos != null)
        {
            player.SendIngameError("createacolony-warehouse-exists", Lang.Get("createacolony:warehouse-exists"));
            return false;
        }
        if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;

        colony.WarehousePos = blockSel.Position.Copy();
        manager.MarkDirty(colony);
        player.SendMessage(0, Lang.Get("createacolony:warehouse-placed", ColonyManager.WarehouseRadius), EnumChatType.Notification);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player) return true;

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        if (colony == null) return true;

        var containers = manager.WarehouseContainers(colony).ToList();
        int items = manager.CountItems(containers, _ => true);
        int slots = containers.Sum(c => c.Inventory.Count);
        int freeSlots = containers.Sum(c => c.Inventory.Count(s => s.Empty));
        player.SendMessage(0, Lang.Get("createacolony:warehouse-summary", containers.Count, items, freeSlots, slots), EnumChatType.Notification);
        return true;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            var colony = manager.GetAt(pos);
            if (colony?.WarehousePos != null && colony.WarehousePos.Equals(pos))
            {
                colony.WarehousePos = null;
                manager.MarkDirty(colony);
            }
        }
        base.OnBlockRemoved(world, pos);
    }
}
