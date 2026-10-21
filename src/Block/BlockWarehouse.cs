using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
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
        manager.AddBuilding(colony, BuildingTypes.Warehouse, blockSel.Position, ColonyManager.RotationFor(player));
        player.SendMessage(0, Lang.Get("createacolony:warehouse-placed", ColonyManager.WarehouseRadius), EnumChatType.Notification);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // With the Builder's Wand in hand the placement window opens instead.
        if (Items.ItemBuildersWand.TryUse(world, byPlayer, blockSel.Position)) return true;
        // The client asks the server for the hut window; the server decides what to show.
        if (world.Side == EnumAppSide.Client)
        {
            world.Api.ModLoader.GetModSystem<ColonyNetwork>().RequestOpenHut(blockSel.Position);
        }
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
                manager.RemoveBuilding(colony, pos);
            }
        }
        base.OnBlockRemoved(world, pos);
    }
}
