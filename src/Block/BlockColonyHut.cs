using CreateAColony.Colonies;
using CreateAColony.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// A hut block: the seed of a building. Place it inside the colony, put a chest next to it, then
/// right-click it to order level 1, and a builder grows the building around it from its blueprint.
/// The building type comes from the block's "buildingType" attribute, so new huts are JSON only.
/// </summary>
public class BlockColonyHut : Block
{
    public string BuildingType => Attributes?["buildingType"].AsString() ?? "";

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

        manager.AddBuilding(colony, BuildingType, blockSel.Position, ColonyManager.RotationFor(player));
        string name = Lang.Get("createacolony:building-" + BuildingType);
        string message = BuildingType is BuildingTypes.Forester or BuildingTypes.Farmer
            ? Lang.Get("createacolony:hut-placed-worker", name, Lang.Get("createacolony:job-" + BuildingType))
            : Lang.Get("createacolony:hut-placed", name);
        player.SendMessage(0, message, EnumChatType.Notification);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
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
            if (colony != null) manager.RemoveBuilding(colony, pos);
        }
        base.OnBlockRemoved(world, pos);
    }
}
