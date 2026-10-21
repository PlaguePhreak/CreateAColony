using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Items;

/// <summary>
/// The Builder's Wand. Right-click the ground to pick a building and see its outline there, then turn
/// and nudge it and press Accept to put its block down from your inventory. Right-click an existing
/// hut, the Town Hall or the Warehouse to see its outline and move it until it is built. With an
/// outline showing, right-clicking anywhere brings its window back.
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
        if (!firstEvent || byEntity is not EntityPlayer { Player: { } player })
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        handling = EnumHandHandling.PreventDefault;
        if (blockSel != null && api.World.BlockAccessor.GetBlock(blockSel.Position) is Blocks.BlockColonyHut or Blocks.BlockTownHall or Blocks.BlockWarehouse)
        {
            TryUse(api.World, player, blockSel.Position);
            return;
        }
        if (api is not ICoreClientAPI capi) return;
        var network = capi.ModLoader.GetModSystem<ColonyNetwork>();
        // In the air or at any other block: back to the window of the outline still showing.
        if (network.ReopenPlacement()) return;
        // Nothing planned yet: plan a building on the spot next to the block clicked.
        if (blockSel != null) network.StartWandPlan(blockSel.Position.AddCopy(blockSel.Face));
        else capi.TriggerIngameError(this, "createacolony-wand", Lang.Get("createacolony:wand-aim-ground"));
    }
}
