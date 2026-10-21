using System.Collections.Generic;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// An early-game camp a colony member builds inside the claim. Once per in-game day it hands out a
/// bundle of starter materials, for a limited number of days, to help get the first buildings up.
/// One camp per colony; the delivery count lives on the colony so re-placing the camp does not reset it.
/// </summary>
public class BlockSupplyCamp : Block
{
    SupplyCampLoot loot = new();
    readonly List<ItemStack> resolvedStacks = new();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api.Side != EnumAppSide.Server) return;

        loot = SupplyCampLoot.Load(api);
        foreach (var entry in loot.Items)
        {
            var code = new AssetLocation(entry.Code);
            CollectibleObject? collectible = (CollectibleObject?)api.World.GetItem(code) ?? api.World.GetBlock(code);
            if (collectible == null)
            {
                api.Logger.Warning("[createacolony] Supply camp item {0} not found, skipping", entry.Code);
                continue;
            }
            resolvedStacks.Add(new ItemStack(collectible, entry.Quantity));
        }
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        if (world.Side == EnumAppSide.Server && byPlayer is IServerPlayer player)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            var colony = manager.GetAt(blockSel.Position);
            if (colony == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member)
            {
                player.SendIngameError("createacolony-supplycamp-outside", Lang.Get("createacolony:supplycamp-outside"));
                return false;
            }
            if (colony.SupplyCampPos != null)
            {
                player.SendIngameError("createacolony-supplycamp-exists", Lang.Get("createacolony:supplycamp-exists"));
                return false;
            }

            if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;

            colony.SupplyCampPos = blockSel.Position.Copy();
            manager.MarkDirty(colony);
            return true;
        }

        return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player) return true;

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        if (colony == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member) return true;

        int today = (int)world.Calendar.TotalDays;
        if (colony.SupplyDeliveriesTaken >= loot.Deliveries)
        {
            player.SendMessage(0, Lang.Get("createacolony:supplycamp-exhausted"), EnumChatType.Notification);
            return true;
        }
        if (colony.LastSupplyDay >= today)
        {
            player.SendMessage(0, Lang.Get("createacolony:supplycamp-tomorrow"), EnumChatType.Notification);
            return true;
        }

        foreach (var stack in resolvedStacks)
        {
            var give = stack.Clone();
            player.Entity.TryGiveItemStack(give);
            // Whatever did not fit in the inventory is dropped on top of the camp.
            if (give.StackSize > 0) world.SpawnItemEntity(give, blockSel.Position.ToVec3d().Add(0.5, 1.1, 0.5));
        }

        colony.LastSupplyDay = today;
        colony.SupplyDeliveriesTaken++;
        manager.MarkDirty(colony);

        int left = loot.Deliveries - colony.SupplyDeliveriesTaken;
        player.SendMessage(0, Lang.Get("createacolony:supplycamp-delivered", left), EnumChatType.Notification);
        return true;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            var colony = manager.GetAt(pos);
            if (colony?.SupplyCampPos != null && colony.SupplyCampPos.Equals(pos))
            {
                colony.SupplyCampPos = null;
                manager.MarkDirty(colony);
            }
        }
        base.OnBlockRemoved(world, pos);
    }
}
