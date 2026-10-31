using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// Berry bush cuttings (Aaron, 0.151.0). A gatherer that carries a knife takes a cutting from a berry bush the way a
/// player does (sneak + right-click with a knife): one per bush a year, without harming the bush. It keeps config
/// GathererCuttingsKeep of each kind in the hut and warehouse, so players can plant new bushes.
/// </summary>
public partial class ColonyManager
{
    /// <summary>The cutting a berry bush gives (its "cuttingBlockCode"), or null when it gives none.</summary>
    Block? CuttingBlock(BlockEntity? be)
    {
        if (be?.GetBehavior<BEBehaviorFruitingBush>() == null) return null;
        string? code = be.Block.Attributes?["cuttingBlockCode"].AsString();
        return string.IsNullOrEmpty(code) ? null : sapi.World.GetBlock(AssetLocation.Create(code, be.Block.Code.Domain));
    }

    /// <summary>True when a cutting can be taken from this bush now: none was taken in the last year (as the game counts it).</summary>
    bool CanTakeCutting(BlockEntity? be)
    {
        if (!Config.GathererTakesCuttings || CuttingBlock(be) == null || be!.GetBehavior<BEBehaviorFruitingBush>() is not { } bush) return false;
        double years = (sapi.World.Calendar.TotalDays - bush.BState.LastCuttingTakenTotalDays) / sapi.World.Calendar.DaysPerYear;
        return years >= (YearsBetweenCuttingsField?.GetValue(bush) as double? ?? 1.0);
    }

    static readonly System.Reflection.FieldInfo? YearsBetweenCuttingsField =
        typeof(BEBehaviorFruitingBush).GetField("yearsBetweenCuttings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    /// <summary>True when the colony has config GathererCuttingsKeep of this bush's cutting already (warehouse and hut). Checked every 30 seconds.</summary>
    bool CuttingsStocked(Colony colony, Building hut, Block cutting)
    {
        long now = sapi.World.ElapsedMilliseconds;
        string key = "cutting-" + cutting.Code;
        if (herbStock.TryGetValue((hut.BuildingId, key), out var cached) && now - cached.Ms < 30_000) return cached.Stocked;
        ItemMatch same = s => s.Collectible.Code == cutting.Code;
        bool stocked = CountItems(WarehouseContainers(colony), same) + CountItems(HutStorage(colony, hut), same) >= Config.GathererCuttingsKeep;
        herbStock[(hut.BuildingId, key)] = (now, stocked);
        return stocked;
    }

    /// <summary>Takes a cutting from the bush into the gatherer's inventory, as BEBehaviorFruitingBush.SetHarvested does for a player.</summary>
    bool TakeCutting(Colony colony, Building hut, CitizenRecord worker, BlockPos pos)
    {
        var be = sapi.World.BlockAccessor.GetBlockEntity(pos);
        if (!CanTakeCutting(be) || CuttingBlock(be) is not { } cutting || be!.GetBehavior<BEBehaviorFruitingBush>() is not { } bush) return false;
        var stack = new ItemStack(cutting);
        stack.Attributes.SetString("traits", string.Join(",", bush.BState.Traits ?? Array.Empty<string>()));
        bush.BState.LastCuttingTakenTotalDays = sapi.World.Calendar.TotalDays;
        be.MarkDirty(true);
        herbStock.Remove((hut.BuildingId, "cutting-" + cutting.Code));
        StoreHarvest(colony, hut, worker, new[] { stack }, pos);
        return true;
    }
}
