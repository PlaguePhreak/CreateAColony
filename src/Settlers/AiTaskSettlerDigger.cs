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
/// What miners and quarriers share. Their own tool is the pickaxe, but like a player they use the right
/// tool for each block: a shovel for soil, sand and gravel when they have one, the pickaxe for rock and
/// ore. Of the tools they carry, the one fastest on the block's material wins (the game's own mining
/// speeds), so modded tools fit in too. A block too hard for every tool they carry waits until a better
/// pick turns up in the hut's storage, which they then fetch and use.
/// </summary>
public abstract class AiTaskSettlerDigger : AiTaskSettlerWorker
{
    protected AiTaskSettlerDigger(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override EnumTool Tool => EnumTool.Pickaxe;
    protected override ItemMatch Stock => _ => false;

    /// <summary>The tools besides the pickaxe a digger takes along when the hut's storage has them.</summary>
    static readonly EnumTool[] HelperTools = { EnumTool.Shovel };

    /// <summary>A pickaxe or shovel (never a prospecting pick, which is a different kind of tool).</summary>
    protected static bool IsDiggingTool(ItemStack? stack) =>
        stack?.Collectible.Tool is EnumTool.Pickaxe or EnumTool.Shovel && !ColonyManager.IsProspectingPick(stack);

    /// <summary>How fast a tool breaks a block, from the game's mining speeds for its material; 1 when it has none (as bare hands).</summary>
    public static float SpeedOn(ItemStack stack, Block block) =>
        stack.Collectible.MiningSpeed != null && stack.Collectible.MiningSpeed.TryGetValue(block.BlockMaterial, out float speed) ? speed : 1;

    /// <summary>Diggers keep their shovel too, rather than leaving it in the hut's storage with the harvest.</summary>
    protected override bool AlsoKeeps(ItemStack stack) => IsDiggingTool(stack);

    /// <summary>
    /// The best tool the worker carries (in hand or inventory) for a block: one hard enough for it, the
    /// fastest on its material, the better tier between equals. Null when none is hard enough.
    /// </summary>
    protected ItemStack? BestToolFor(CitizenRecord citizen, Block block)
    {
        var carried = new[] { Hand?.Itemstack }.Concat(manager!.CarriedItems(citizen));
        return carried.Where(s => IsDiggingTool(s) && s!.Collectible.ToolTier >= block.RequiredMiningTier)
            .OrderByDescending(s => SpeedOn(s!, block)).ThenByDescending(s => s!.Collectible.ToolTier)
            .FirstOrDefault();
    }

    /// <summary>For digging: the best tool for the block at the target, so the right one is in hand from the first swing.</summary>
    protected override bool WantsInHand(ItemStack? stack)
    {
        var block = entity.World.BlockAccessor.GetBlock(target);
        if (block.Id == 0 || Citizen is not { } citizen || BestToolFor(citizen, block) is not { } best) return IsToolStack(stack);
        return stack?.Collectible == best.Collectible;
    }

    /// <summary>
    /// Seconds to dig the target: the job's time with the right tool, up to twice as long with a slow one
    /// (a pickaxe in soil), down to half with a fast one.
    /// </summary>
    protected float ToolSeconds(float seconds)
    {
        var block = entity.World.BlockAccessor.GetBlock(target);
        if (block.Id == 0 || Citizen is not { } citizen || BestToolFor(citizen, block) is not { } best) return seconds;
        return seconds * GameMath.Clamp(3f / SpeedOn(best, block), 0.5f, 2f);
    }

    /// <summary>The best pickaxe tier the worker has in hand or inventory, -1 for none.</summary>
    protected int BestPickTier(CitizenRecord citizen)
    {
        int hand = IsToolStack(Hand?.Itemstack) ? Hand!.Itemstack.Collectible.ToolTier : -1;
        int pack = manager!.CarriedItems(citizen).Where(s => IsToolStack(s)).Select(s => s.Collectible.ToolTier).DefaultIfEmpty(-1).Max();
        return System.Math.Max(hand, pack);
    }

    /// <summary>
    /// Walks to fetch a better pickaxe than any the worker has, or a shovel when it has none, when the
    /// hut's storage holds one.
    /// </summary>
    protected bool FetchBetterPick(CitizenRecord citizen, List<BlockEntityContainer> storage)
    {
        int have = BestPickTier(citizen);
        bool Better(ItemStack? s) => IsToolStack(s) && s!.Collectible.ToolTier > have;
        var chest = storage.FirstOrDefault(c => c.Inventory.Any(slot => Better(slot.Itemstack)));
        if (chest != null) return FetchSupplies(chest.Pos, s => Better(s), 1);

        var carried = new[] { Hand?.Itemstack }.Concat(manager!.CarriedItems(citizen)).ToList();
        foreach (var kind in HelperTools)
        {
            if (carried.Any(s => s?.Collectible.Tool == kind)) continue;
            bool Helper(ItemStack? s) => s?.Collectible.Tool == kind && IsDiggingTool(s);
            chest = storage.FirstOrDefault(c => c.Inventory.Any(slot => Helper(slot.Itemstack)));
            if (chest != null) return FetchSupplies(chest.Pos, s => Helper(s), 1);
        }
        return false;
    }

    /// <summary>Holds this tool, from the worker's inventory, unless one like it is in hand already.</summary>
    void Equip(Colony colony, CitizenRecord citizen, ItemStack tool)
    {
        var hand = Hand;
        if (hand == null || hand.Itemstack?.Collectible == tool.Collectible) return;
        var taken = manager!.TakeFromPack(colony, citizen, s => s.Collectible == tool.Collectible, 1);
        if (taken == null) return;
        PutAwayHand(colony, citizen);
        hand.Itemstack = taken;
        SyncHand();
    }

    /// <summary>
    /// Digs out the target with the best tool the worker carries for it. A block too hard for all of them
    /// is not dug: couriers are asked for a pick good enough and false is returned, so the job can work
    /// elsewhere meanwhile.
    /// </summary>
    protected bool Dig(Colony colony, CitizenRecord citizen, Building hut)
    {
        var block = entity.World.BlockAccessor.GetBlock(target);
        if (block.Id == 0) return true;
        if (BestToolFor(citizen, block) is not { } tool)
        {
            if (BestPickTier(citizen) < 0) return true; // no pick at all: the worker fetches one first
            manager!.EnsureToolRequest(colony, hut, hut, new ColonyManager.ToolNeed(EnumTool.Pickaxe, block.RequiredMiningTier));
            SetStatus("needs-better-pick");
            return false;
        }
        Equip(colony, citizen, tool);
        if (Hand is not { Itemstack: not null } hand) return true;
        manager!.DigBlock(colony, hut, citizen, entity, target, hand);
        SyncHand(); // a tool that broke must vanish from the model too
        return true;
    }
}
