using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// A work station (ColonyManager.Stations.cs): put down by hand inside a building it may stand in (the
/// Healer's Station inside a Church's zone), it adds one job there. It has no building of its own, so it never
/// goes down with the Builder's Wand. Right-click opens its window and sneak + right-click its own small storage,
/// as for a hut. The job comes from the "buildingType" attribute, as for huts.
/// </summary>
public class BlockWorkStation : BlockColonyHut
{
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        // With PlaceHutsByHand on it is only a block, for making blueprints.
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player || WandOnly.HandPlacing(world))
        {
            if (!PlacePlainBlock(world, byPlayer, blockSel, byItemStack)) return false;
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityColonyHut be) be.SetRotation(WandOnly.HandRotation(byPlayer));
            return true;
        }

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        if (colony == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member)
        {
            player.SendIngameError("createacolony-outside", Lang.GetL(player.LanguageCode, "createacolony:marker-outside"));
            return false;
        }
        var host = manager.StationHostAt(colony, BuildingType, blockSel.Position);
        if (host == null)
        {
            string hosts = string.Join(", ", (ColonyManager.StationHosts.GetValueOrDefault(BuildingType) ?? System.Array.Empty<string>())
                .Select(t => Lang.GetL(player.LanguageCode, "createacolony:building-" + t)));
            player.SendIngameError("createacolony-station-host", Lang.GetL(player.LanguageCode, "createacolony:station-needs-host", hosts));
            return false;
        }
        if (!PlacePlainBlock(world, byPlayer, blockSel, byItemStack)) return false;
        int rotation = ColonyManager.RotationFor(player);
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityColonyHut hut) hut.SetRotation(rotation);
        manager.AddStation(colony, BuildingType, blockSel.Position, host);
        player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:station-placed-" + BuildingType,
            Lang.GetL(player.LanguageCode, "createacolony:building-" + BuildingType), ColonyManager.BuildingName(host)), EnumChatType.Notification);
        return true;
    }


    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer.Entity.Controls.ShiftKey)
        {
            if (world.Side == EnumAppSide.Server && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityColonyHut storage)
            {
                storage.OnPlayerRightClick(byPlayer, blockSel);
            }
            return true;
        }
        if (world.Side == EnumAppSide.Client)
        {
            world.Api.ModLoader.GetModSystem<Network.ColonyNetwork>().RequestOpenHut(blockSel.Position);
        }
        return true;
    }
}
