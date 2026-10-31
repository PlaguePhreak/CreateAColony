using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Cattail, papyrus and tule roots (Aaron, 0.148.0). When the colony runs low on food the Gatherer digs up reeds
/// for their roots (never the last few of a patch) and roasts them on a fire pit by its hut, the way a player
/// cooks them on a fire pit. The roasted roots go into the hut storage, where hungry settlers find them.
/// </summary>
public partial class ColonyManager
{
    /// <summary>
    /// True when the colony's food is low: everything settlers could eat in the colony fills fewer than config
    /// GathererRootsFoodDays full bellies per settler. Worked out again every minute.
    /// </summary>
    public bool FoodLow(Colony colony, EntityAgent eater)
    {
        long now = sapi.World.ElapsedMilliseconds;
        if (foodLow.TryGetValue(colony.ColonyId, out var cached) && now - cached.Ms < 60_000) return cached.Low;
        int people = colony.Citizens.Count(c => c.Alive);
        double wanted = people * Config.SaturationMax * Config.GathererRootsFoodDays;
        double have = 0;
        foreach (var container in FindBlockEntities<BlockEntityContainer>(colony))
        {
            foreach (var slot in container.Inventory)
            {
                if (slot.Empty) continue;
                if (EvaluateFood(container, slot, eater) is not { } food) continue;
                int portions = slot.Itemstack.Block is IBlockMealContainer meal
                    ? Math.Max(1, (int)meal.GetQuantityServings(sapi.World, slot.Itemstack))
                    : slot.Itemstack.StackSize;
                have += food.Satiety * portions;
                if (have >= wanted) break;
            }
            if (have >= wanted) break;
        }
        bool low = people > 0 && have < wanted;
        foodLow[colony.ColonyId] = (now, low);
        return low;
    }

    readonly Dictionary<string, (long Ms, bool Low)> foodLow = new();

    /// <summary>What digging up a reed gives: what it drops, and its root if the drops leave that out.</summary>
    ItemStack[] RootDrops(Block block, BlockPos pos)
    {
        var drops = DropsOf(block, pos).ToList();
        if (!drops.Any(IsReedRoot) && sapi.World.GetItem(new AssetLocation(block.Code.Domain, (block.FirstCodePart(1) == "coopersreed" ? "cattail" : block.FirstCodePart(1)) + "root")) is { } root)
        {
            drops.Add(new ItemStack(root));
        }
        return drops.ToArray();
    }

    /// <summary>A root a gatherer roasts: one of the reeds' roots (cattail, papyrus, tule) that cooks on a fire pit.</summary>
    public bool IsCookableRoot(ItemStack stack) => IsReedRoot(stack) && IsGrillable(stack);

    static bool IsReedRoot(ItemStack stack) => stack.Collectible is ItemCattailRoot || stack.Collectible.Code.Path.EndsWith("root");

    /// <summary>
    /// True when the gatherer may dig up the reed here for its root: food is low (as its gatherer last found,
    /// FoodLow), and at least config GathererRootsLeave other reeds grow within 4 blocks, so the patch is never dug out.
    /// </summary>
    bool DigsRoots(Colony colony, BlockPos pos)
    {
        if (!Config.GathererDigsRoots || !foodLow.TryGetValue(colony.ColonyId, out var food) || !food.Low) return false;
        var ba = sapi.World.BlockAccessor;
        int others = 0;
        var at = new BlockPos(0);
        for (int dx = -4; dx <= 4; dx++)
        for (int dz = -4; dz <= 4; dz++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dz == 0 && dy == 0) continue;
            at.Set(pos.X + dx, pos.Y + dy, pos.Z + dz);
            if (ba.GetBlock(at) is BlockReeds && ++others >= Config.GathererRootsLeave) return true;
        }
        return false;
    }
}
