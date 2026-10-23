using System.Linq;
using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// Placing a Town Hall (with the Builder's Wand) founds a colony and creates its land claim. Placement
/// is refused, and the item kept, if the player already owns a colony or the area overlaps another claim.
/// </summary>
public class BlockTownHall : Block, IWandPlaceable
{
    /// <summary>The Town Hall is put down with the Builder's Wand, which shows the building first.</summary>
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        // With PlaceHutsByHand on it is only a block, for making blueprints: no colony, no building.
        if (WandOnly.HandPlacing(world))
        {
            if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTownHall be) be.SetRotation(WandOnly.HandRotation(byPlayer));
            return true;
        }
        WandOnly.Tell(world, byPlayer);
        return false;
    }

    public string? PlaceByWand(IServerPlayer player, BlockPos pos, int rotation)
    {
        var world = player.Entity.World;
        if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(this)) return "createacolony:wand-spot-taken";
        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        string name = Lang.GetL(player.LanguageCode, "createacolony:default-colony-name", player.PlayerName);
        var result = manager.TryFoundColony(player, pos, name, out var colony, rotation);
        if (result != FoundColonyResult.Ok) return "createacolony:found-error-" + result.ToString().ToLowerInvariant();

        world.BlockAccessor.SetBlock(BlockId, pos);
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTownHall be)
        {
            be.ColonyId = colony!.ColonyId;
            be.Rotation = rotation;
            be.MarkDirty(true);
        }
        player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:founded", colony!.Name), EnumChatType.Notification);
        manager.SpawnStartingSettlers(colony);
        player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:settlers-arrived", colony.Citizens.Count, colony.Name), EnumChatType.Notification);
        return null;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // With the Builder's Wand in hand the placement window opens instead.
        if (Items.ItemBuildersWand.TryUse(world, byPlayer, blockSel.Position)) return true;
        // The client asks the server for the Town Hall window; the server decides what to show.
        if (world.Side == EnumAppSide.Client)
        {
            world.Api.ModLoader.GetModSystem<ColonyNetwork>().RequestOpen(blockSel.Position);
        }
        return true;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTownHall { ColonyId: not null } be)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            var colony = manager.GetById(be.ColonyId);
            if (colony != null) manager.Disband(colony);
        }
        base.OnBlockRemoved(world, pos);
    }
}
