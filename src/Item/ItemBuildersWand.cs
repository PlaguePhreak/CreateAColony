using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Items;

/// <summary>
/// The Builder's Wand: right-click a hut, the Town Hall or the Warehouse with it to see the outline
/// of the building's highest level and open the placement window, where the building can be turned
/// and raised or lowered until it is built.
/// </summary>
public class ItemBuildersWand : Item
{
    public static bool IsHeldBy(IPlayer player) => player.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible is ItemBuildersWand;

    /// <summary>Opens the placement window for the building at pos if the player holds the wand. True when it did, or would on the server.</summary>
    public static bool TryUse(IWorldAccessor world, IPlayer player, BlockPos pos)
    {
        if (!IsHeldBy(player)) return false;
        if (world.Side != EnumAppSide.Server || player is not IServerPlayer serverPlayer) return true;

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(pos);
        var building = colony == null ? null : manager.GetBuilding(colony, pos);
        if (colony == null || building == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member)
        {
            serverPlayer.SendIngameError("createacolony-wand", Lang.GetL(serverPlayer.LanguageCode, "createacolony:wand-not-building"));
            return true;
        }
        world.Api.ModLoader.GetModSystem<ColonyNetwork>().OpenPlacement(serverPlayer, colony, building);
        return true;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (firstEvent && blockSel != null && byEntity is EntityPlayer { Player: { } player }
            && api.World.BlockAccessor.GetBlock(blockSel.Position) is Blocks.BlockColonyHut or Blocks.BlockTownHall or Blocks.BlockWarehouse)
        {
            TryUse(api.World, player, blockSel.Position);
            handling = EnumHandHandling.PreventDefault;
            return;
        }
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
}
