using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// Placing a Town Hall founds a colony and creates its land claim. Placement is refused
/// (and the item kept) if the player already owns a colony or the area overlaps another claim.
/// </summary>
public class BlockTownHall : Block
{
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        if (world.Side == EnumAppSide.Server && byPlayer is IServerPlayer player)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            string name = Lang.Get("createacolony:default-colony-name", player.PlayerName);
            var result = manager.TryFoundColony(player, blockSel.Position, name, out var colony);
            if (result != FoundColonyResult.Ok)
            {
                player.SendIngameError("createacolony-" + result.ToString().ToLowerInvariant(),
                    Lang.Get("createacolony:found-error-" + result.ToString().ToLowerInvariant()));
                return false;
            }

            if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack))
            {
                manager.Disband(colony!);
                return false;
            }

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTownHall be)
            {
                be.ColonyId = colony!.ColonyId;
                be.MarkDirty();
            }
            player.SendMessage(0, Lang.Get("createacolony:founded", colony!.Name), EnumChatType.Notification);

            manager.SpawnStartingSettlers(colony);
            player.SendMessage(0, Lang.Get("createacolony:settlers-arrived", colony.Citizens.Count, colony.Name), EnumChatType.Notification);
            return true;
        }

        return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // MVP: chat summary. The Town Hall GUI (ColonyClientSystem) opens here once it exists.
        if (world.Side == EnumAppSide.Server && byPlayer is IServerPlayer player
            && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityTownHall { ColonyId: not null } be)
        {
            var colony = world.Api.ModLoader.GetModSystem<ColonyManager>().GetById(be.ColonyId);
            if (colony != null)
            {
                player.SendMessage(0, Lang.Get("createacolony:summary", colony.Name, colony.Level, colony.Citizens.Count(c => c.Alive), colony.Members.Count), EnumChatType.Notification);
            }
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
