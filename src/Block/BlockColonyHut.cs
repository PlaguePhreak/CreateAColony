using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// A hut block: the seed of a building. Place it inside the colony, put a chest next to it, then
/// order level 1 from the Town Hall and a builder grows the building around it from its blueprint.
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
        player.SendMessage(0, Lang.Get("createacolony:hut-placed", Lang.Get("createacolony:building-" + BuildingType)), EnumChatType.Notification);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player) return true;

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        var building = colony == null ? null : manager.GetBuilding(colony, blockSel.Position);
        if (building == null) return true;

        string name = Lang.Get("createacolony:building-" + building.Type);
        player.SendMessage(0, building.HasWorkOrder
            ? Lang.Get("createacolony:hut-building", name, building.Level, building.TargetLevel)
            : Lang.Get("createacolony:hut-info", name, building.Level), EnumChatType.Notification);
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
