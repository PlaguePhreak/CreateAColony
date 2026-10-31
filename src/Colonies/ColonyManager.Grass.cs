using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// Gatherers cut tall grass for dry grass when the colony needs it (Aaron, 0.121.0): when huts are waiting for dry
/// grass (a builder's fire pit, a crafter's input, Minimum stock) and the warehouse, the Gatherer's Huts and the
/// gatherers' packs together have too little. The grass is cut the way a knife does, so it grows back.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How much dry grass each colony is short of, and when that was worked out.</summary>
    readonly Dictionary<string, (long Ms, int Short)> grassShort = new();
    /// <summary>Which grass blocks give something when cut with a knife.</summary>
    readonly Dictionary<int, bool> grassGivesHay = new();

    static readonly AssetLocation DryGrassCode = new("game:drygrass");

    /// <summary>How much more dry grass the colony needs than it has, 0 when none; worked out at most every 5 seconds.</summary>
    public int GrassShort(Colony colony)
    {
        if (!Config.GathererCutsGrass) return 0;
        long now = sapi.World.ElapsedMilliseconds;
        if (grassShort.TryGetValue(colony.ColonyId, out var cached) && now - cached.Ms < 5000) return cached.Short;
        int shortBy = 0;
        if (sapi.World.GetItem(DryGrassCode) is { } dry)
        {
            var sample = new ItemStack(dry);
            int wanted = colony.Requests.Where(r => r.Remaining > 0 && Matcher(r.ItemCode)(sample)).Sum(r => r.Remaining);
            if (wanted > 0)
            {
                System.Func<ItemStack, bool> isDry = s => s?.Collectible?.Code == dry.Code;
                int have = CountItems(WarehouseContainers(colony), isDry);
                foreach (var hut in colony.Buildings.Where(b => b.Type == BuildingTypes.Gatherer))
                {
                    have += CountItems(HutStorage(colony, hut), isDry);
                    if (WorkerAt(colony, hut, Jobs.Gatherer) is { } worker) have += PackItems(worker).Where(isDry).Sum(s => s.StackSize);
                }
                shortBy = Math.Max(0, wanted - have);
            }
        }
        grassShort[colony.ColonyId] = (now, shortBy);
        return shortBy;
    }

    /// <summary>True when a block is tall grass that gives dry grass when cut, and has not been cut yet.</summary>
    bool IsCuttableGrass(Block block)
    {
        if (block.FirstCodePart() != "tallgrass") return false;
        string? height = block.Variant["tallgrass"];
        if (height == null || height == "eaten") return false;
        if (!grassGivesHay.TryGetValue(block.Id, out bool gives))
        {
            gives = (block.Drops ?? Array.Empty<BlockDropItemStack>()).Any(d => (d.Tool is null or EnumTool.Knife) && d.ResolvedItemstack != null);
            grassGivesHay[block.Id] = gives;
        }
        return gives;
    }

    /// <summary>
    /// The nearest tall grass to cut, near the gatherer and inside its hut's range, not in or beside a building.
    /// Null when there is none.
    /// </summary>
    public BlockPos? NextGrass(Colony colony, Building hut, Vec3d from, System.Func<BlockPos, bool> skip)
    {
        if (!MayCollect(hut, "grass")) return null;
        var ba = sapi.World.BlockAccessor;
        int range = Math.Max(4, GatherRadius(hut)), look = Math.Max(4, Config.GrassSearchRadius);
        long rangeSq = (long)range * range;
        var pos = new BlockPos(0);
        BlockPos? best = null;
        double bestDist = double.MaxValue;
        // Around the gatherer first; around the hut when it is out of range or there is nothing near it.
        foreach (var center in new[] { from.AsBlockPos, hut.HutPos })
        {
            for (int dx = -look; dx <= look; dx++)
            for (int dz = -look; dz <= look; dz++)
            {
                int x = center.X + dx, z = center.Z + dz;
                long hx = x - hut.HutPos.X, hz = z - hut.HutPos.Z;
                if (hx * hx + hz * hz > rangeSq) continue;
                pos.Set(x, 0, z);
                if (ba.GetChunkAtBlockPos(pos) == null) continue;
                pos.Y = ba.GetRainMapHeightAt(pos) + 1;
                if (!IsCuttableGrass(ba.GetBlock(pos))) continue;
                double dist = pos.DistanceSqTo(from.X, from.Y, from.Z);
                if (dist >= bestDist || skip(pos) || !CanWorkAt(colony, pos) || NearBuilding(colony, pos)) continue;
                best = pos.Copy();
                bestDist = dist;
            }
            if (best != null) return best;
        }
        return null;
    }

    /// <summary>Cuts the grass at the spot for dry grass, leaving short stubble that grows back. False when there was nothing to cut.</summary>
    public bool CutGrass(Colony colony, Building hut, CitizenRecord worker, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var block = ba.GetBlock(pos);
        if (!IsCuttableGrass(block) || !MayCollect(hut, "grass")) return false;
        var drops = ToolDrops(block, EnumTool.Knife);
        var cut = sapi.World.GetBlock(block.CodeWithVariant("tallgrass", "eaten"));
        if (cut == null || cut.Id == 0) BreakQuietly(pos);
        else
        {
            ba.SetBlock(cut.BlockId, pos);
            ba.MarkBlockDirty(pos);
        }
        StoreHarvest(colony, hut, worker, drops, pos);
        int got = drops.Where(d => d.Collectible.Code == DryGrassCode).Sum(d => d.StackSize);
        if (grassShort.TryGetValue(colony.ColonyId, out var cached)) grassShort[colony.ColonyId] = (cached.Ms, Math.Max(0, cached.Short - got));
        AddStat(hut, "grass-cut");
        MarkDirty(colony);
        return true;
    }

    /// <summary>
    /// What a block drops as if a player broke it holding this tool (null: whatever drops it lists, any tool), for
    /// blocks whose drops depend on the player's tool, which <see cref="DropsOf"/> cannot give without a player.
    /// </summary>
    ItemStack[] ToolDrops(Block block, EnumTool? tool) => (block.Drops ?? Array.Empty<BlockDropItemStack>())
        .Where(d => tool == null || d.Tool == null || d.Tool == tool)
        .Select(d => d.GetNextItemStack())
        .Where(s => s != null && s.StackSize > 0)
        .Select(s => s!)
        .ToArray();
}
