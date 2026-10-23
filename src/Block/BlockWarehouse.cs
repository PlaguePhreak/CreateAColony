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
public class BlockWarehouse : Block, IWandPlaceable
{
    /// <summary>The Warehouse is put down with the Builder's Wand, which shows the building first.</summary>
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        // With PlaceHutsByHand on it is only a block, for making blueprints: no colony, no building.
        if (WandOnly.HandPlacing(world)) return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
        WandOnly.Tell(world, byPlayer);
        return false;
    }

    public string? PlaceByWand(IServerPlayer player, BlockPos pos, int rotation)
    {
        var world = player.Entity.World;
        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(pos);
        if (colony == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member) return "createacolony:marker-outside";
        if (colony.WarehousePos != null) return "createacolony:warehouse-exists";
        if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(this)) return "createacolony:wand-spot-taken";

        world.BlockAccessor.SetBlock(BlockId, pos);
        colony.WarehousePos = pos.Copy();
        manager.AddBuilding(colony, BuildingTypes.Warehouse, pos, rotation);
        player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:warehouse-placed", ColonyManager.WarehouseRadius), EnumChatType.Notification);
        return null;
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
