using System.Collections.Generic;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony.Blocks;

/// <summary>
/// The very first step: a camp placed anywhere, no colony needed. A player's first camp in a world hands
/// them the colony charter (Town Hall, Builder's Hut, Builder's Wand and Colony Ledger, from
/// config/supplycamp.json) and starts with one set of materials in its own storage, opened like a chest.
/// If there is a blueprint for the camp, its building goes up around it at once. Any later camp, from
/// breaking and re-placing it or crafting another, is only an empty chest.
/// </summary>
public class BlockSupplyCamp : Block
{
    List<ItemStack> playerStacks = new();
    List<ItemStack> campStacks = new();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        if (api.Side != EnumAppSide.Server) return;

        var loot = SupplyCampLoot.Load(api);
        playerStacks = SupplyCampLoot.Resolve(api, loot.PlayerItems);
        campStacks = SupplyCampLoot.Resolve(api, loot.Items);
    }

    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player) return true;

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var pos = blockSel.Position;
        if (manager.HasStarterKit(player.PlayerUID))
        {
            player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:supplycamp-empty"), EnumChatType.Notification);
            return true;
        }
        manager.MarkStarterKit(player.PlayerUID);

        foreach (var stack in playerStacks)
        {
            var give = stack.Clone();
            player.Entity.TryGiveItemStack(give);
            // Whatever did not fit in the inventory is dropped on top of the camp.
            if (give.StackSize > 0) world.SpawnItemEntity(give, pos.ToVec3d().Add(0.5, 1.1, 0.5));
        }

        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityGenericTypedContainer storage)
        {
            int slot = 0;
            foreach (var stack in campStacks)
            {
                if (slot >= storage.Inventory.Count)
                {
                    world.SpawnItemEntity(stack.Clone(), pos.ToVec3d().Add(0.5, 1.1, 0.5));
                    continue;
                }
                storage.Inventory[slot++].Itemstack = stack.Clone();
            }
            storage.MarkDirty(true);
        }

        if (manager.BuildSupplyCamp(player, pos) is { } why)
        {
            player.SendMessage(0, Lang.GetL(player.LanguageCode, why), EnumChatType.Notification);
        }
        player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:supplycamp-kit"), EnumChatType.Notification);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var ba = world.BlockAccessor;
        // Camps placed before 0.32 had no storage; they get an empty one now.
        if (world.Side == EnumAppSide.Server && ba.GetBlockEntity(blockSel.Position) == null && EntityClass != null)
        {
            ba.SpawnBlockEntity(EntityClass, blockSel.Position);
        }
        // Opened like a chest: the server sends the inventory window.
        if (world.Side == EnumAppSide.Server && ba.GetBlockEntity(blockSel.Position) is BlockEntityGenericTypedContainer storage)
        {
            storage.OnPlayerRightClick(byPlayer, blockSel);
        }
        return true;
    }
}
