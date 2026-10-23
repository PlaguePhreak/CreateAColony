using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// What miners and quarriers share: they dig with a pickaxe, and a block too hard for their pick (its
/// mining tier above the pick's) waits until a better pick turns up in the hut's storage, which they
/// then fetch and use.
/// </summary>
public abstract class AiTaskSettlerDigger : AiTaskSettlerWorker
{
    protected AiTaskSettlerDigger(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override EnumTool Tool => EnumTool.Pickaxe;
    protected override ItemMatch Stock => _ => false;

    /// <summary>The best pickaxe tier the worker has in hand or inventory, -1 for none.</summary>
    protected int BestPickTier(CitizenRecord citizen)
    {
        int hand = IsToolStack(Hand?.Itemstack) ? Hand!.Itemstack.Collectible.ToolTier : -1;
        int pack = manager!.PackItems(citizen).Where(s => IsToolStack(s)).Select(s => s.Collectible.ToolTier).DefaultIfEmpty(-1).Max();
        return System.Math.Max(hand, pack);
    }

    /// <summary>Walks to fetch a better pickaxe than any the worker has, when the hut's storage holds one.</summary>
    protected bool FetchBetterPick(CitizenRecord citizen, List<BlockEntityContainer> storage)
    {
        int have = BestPickTier(citizen);
        bool Better(ItemStack? s) => IsToolStack(s) && s!.Collectible.ToolTier > have;
        var chest = storage.FirstOrDefault(c => c.Inventory.Any(slot => Better(slot.Itemstack)));
        return chest != null && FetchSupplies(chest.Pos, s => Better(s), 1);
    }

    /// <summary>Holds the best pickaxe the worker carries.</summary>
    void EquipBestPick(Colony colony, CitizenRecord citizen)
    {
        var hand = Hand;
        if (hand == null) return;
        int held = IsToolStack(hand.Itemstack) ? hand.Itemstack!.Collectible.ToolTier : -1;
        int best = BestPickTier(citizen);
        if (best <= held) return;
        var better = manager!.TakeFromPack(colony, citizen, s => IsToolStack(s) && s.Collectible.ToolTier == best, 1);
        if (better == null) return;
        PutAwayHand(colony, citizen);
        hand.Itemstack = better;
        SyncHand();
    }

    /// <summary>
    /// Digs out the target with the pick in hand. A block too hard for the pick is not dug: couriers are
    /// asked for a pick good enough and false is returned, so the job can work elsewhere meanwhile.
    /// </summary>
    protected bool Dig(Colony colony, CitizenRecord citizen, Building hut)
    {
        EquipBestPick(colony, citizen);
        var pick = Hand;
        if (pick == null || !HoldsTool) return true;
        var block = entity.World.BlockAccessor.GetBlock(target);
        if (block.Id == 0) return true;
        if (block.RequiredMiningTier > pick.Itemstack!.Collectible.ToolTier)
        {
            manager!.EnsureToolRequest(colony, hut, hut, new ColonyManager.ToolNeed(EnumTool.Pickaxe, block.RequiredMiningTier));
            SetStatus("needs-better-pick");
            return false;
        }
        manager!.DigBlock(colony, hut, citizen, entity, target, pick);
        SyncHand(); // a pick that broke must vanish from the model too
        return true;
    }
}
