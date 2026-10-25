using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// The forester's trade. Trees are found and felled with the game's own axe logic, so a forester
/// fells exactly what a player's axe would: the whole tree with its leaves, never a log someone placed
/// in a wall. The stump is replanted with a tree seed or sapling of the same wood when it has one.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How far below the treetops a forester's search looks for a trunk's foot.</summary>
    const int TreeScanDepth = 40;
    /// <summary>Smaller "trees" (a bush, a lone log) are not worth the walk.</summary>
    const int MinTreeLogs = 3;

    ItemAxe? axeLogic;

    /// <summary>Any axe item; its FindTree works out which blocks belong to a tree.</summary>
    ItemAxe? AxeLogic => axeLogic ??= sapi.World.Items.OfType<ItemAxe>().FirstOrDefault();

    /// <summary>A tree seed or a sapling, either of which a forester can plant.</summary>
    public static bool IsTreeSeed(ItemStack stack) =>
        stack.Collectible is ItemTreeSeed || stack.Collectible.Code?.Path.StartsWith("sapling-") == true;

    /// <summary>A log that grew in the wild: part of a tree the axe can fell, never a placed log.</summary>
    static bool IsTreeLog(Block block) =>
        block.BlockMaterial == EnumBlockMaterial.Wood
        && block.Attributes?["treeFellingGroupSpreadIndex"].AsInt(0) >= 2
        && block.Attributes?["treeFellingGroupCode"].Exists == true
        && block.Code?.Path.Contains("placed") == false;

    /// <summary>A wild log standing on soil: the foot of a tree, where a forester cuts.</summary>
    bool IsTrunkBase(BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        if (!IsTreeLog(ba.GetBlock(pos))) return false;
        var ground = ba.GetBlock(pos.DownCopy());
        return !IsTreeLog(ground) && (ground.Fertility > 0 || ground.BlockMaterial == EnumBlockMaterial.Soil);
    }

    /// <summary>
    /// Looks down each block column of a chunk column from the treetops for trunk feet. Leaves, plants,
    /// snow and air are looked through; anything else is the ground and ends the column.
    /// </summary>
    void ScanTrees(int chunkX, int chunkZ, AreaScanner scanner)
    {
        var ba = sapi.World.BlockAccessor;
        int size = GlobalConstants.ChunkSize;
        var pos = new BlockPos(0);
        for (int x = chunkX * size; x < chunkX * size + size; x++)
        for (int z = chunkZ * size; z < chunkZ * size + size; z++)
        {
            if (!scanner.InRange(x, z)) continue;
            int top = ba.GetRainMapHeightAt(x, z);
            for (int y = top; y > top - TreeScanDepth && y > 0; y--)
            {
                pos.Set(x, y, z);
                var block = ba.GetBlock(pos);
                if (IsTreeLog(block))
                {
                    if (IsTrunkBase(pos))
                    {
                        scanner.Found[pos.Copy()] = true;
                        break;
                    }
                    continue;
                }
                if (block.Id == 0 || block.BlockMaterial is EnumBlockMaterial.Leaves or EnumBlockMaterial.Plant or EnumBlockMaterial.Snow) continue;
                break;
            }
        }
    }

    /// <summary>
    /// The trunk foot of the tree nearest the hut within the forester's range, skipping the given spots.
    /// Trees in land no player is near are kept from the last look and checked once the forester is there.
    /// </summary>
    public BlockPos? FindTree(Colony colony, Building hut, System.Func<BlockPos, bool> skip)
    {
        var ba = sapi.World.BlockAccessor;
        var scanner = Scanner(hut, "trees", ScanTrees);
        foreach (var trunk in scanner.Nearest())
        {
            bool loaded = ba.GetChunkAtBlockPos(trunk) != null;
            if (loaded && (!IsTrunkBase(trunk) || TreeBlocks(trunk).Count(p => IsTreeLog(ba.GetBlock(p))) < MinTreeLogs))
            {
                scanner.Found.Remove(trunk);
                continue;
            }
            // A tree the hut is set not to fell; one in land no player is near is checked once the forester gets there.
            if (loaded && !MayCollect(hut, WoodKey(ba.GetBlock(trunk)))) continue;
            if (skip(trunk) || !CanWorkAt(colony, trunk)) continue;
            return trunk;
        }
        return null;
    }

    /// <summary>Every block an axe would bring down when cutting at this log, leaves last.</summary>
    Stack<BlockPos> TreeBlocks(BlockPos trunk)
    {
        if (AxeLogic == null) return new Stack<BlockPos>();
        return AxeLogic.FindTree(sapi.World, trunk, out _, out _);
    }

    /// <summary>
    /// Fells the tree at the trunk with the axe in the worker's hand, as a player's axe would: each log
    /// wears the axe down, and if the axe breaks the rest of the tree is left standing. What falls goes
    /// into the worker's inventory (or the hut chest). Returns the tree's wood type, or null if nothing fell.
    /// </summary>
    public string? FellTree(Colony colony, Building hut, CitizenRecord worker, EntityAgent entity, BlockPos trunk, ItemSlot axe)
    {
        var ba = sapi.World.BlockAccessor;
        var trunkBlock = ba.GetBlock(trunk);
        if (!IsTreeLog(trunkBlock) || !MayCollect(hut, WoodKey(trunkBlock))) return null;
        string? wood = VariantOf(trunkBlock, "wood");

        var positions = TreeBlocks(trunk);
        if (positions.Count == 0) return null;

        // The same drop chances the axe uses: leaves further out give less.
        float leavesMul = 1;
        float branchyMul = 0.8f;
        int felled = 0;
        var drops = new List<ItemStack>();
        while (positions.Count > 0)
        {
            var pos = positions.Pop();
            var block = ba.GetBlock(pos);
            if (block.Id == 0) continue;
            bool isLog = block.BlockMaterial == EnumBlockMaterial.Wood;
            if (isLog && axe.Empty) continue;
            bool isBranchy = block.Code.Path.Contains("branchy");
            bool isLeaves = block.BlockMaterial == EnumBlockMaterial.Leaves;

            drops.AddRange(DropsOf(block, pos, isLeaves ? leavesMul : isBranchy ? branchyMul : 1));
            BreakQuietly(pos);
            felled++;

            if (isLog) WearTool(entity, axe);
            if (isLeaves && leavesMul > 0.03f) leavesMul *= 0.85f;
            if (isBranchy && branchyMul > 0.015f) branchyMul *= 0.7f;
        }
        if (felled == 0) return null;

        if (felled > 35) sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/treefell"), trunk, -0.25, null, false, 32, GameMath.Clamp(felled / 100f, 0.25f, 1));
        StoreHarvest(colony, hut, worker, drops, trunk);
        AddStat(hut, "trees-felled");
        return wood;
    }

    /// <summary>
    /// Leaves the world grew: never placed ones, and never a fruit tree's foliage or a berry bush, which the
    /// farmer and gatherer pick.
    /// </summary>
    static bool IsWildLeaves(Block block) =>
        block.BlockMaterial == EnumBlockMaterial.Leaves
        && block.Code?.Path.Contains("placed") == false
        && block is not (BlockFruitTreePart or BlockBerryBush)
        && !block.GetType().Name.Contains("Bush");

    /// <summary>True for the ground a bush stands on: anything solid that is not leaves, a log, a plant or snow.</summary>
    bool IsBushGround(BlockPos pos)
    {
        var below = sapi.World.BlockAccessor.GetBlock(pos);
        return below.Id != 0 && !IsWildLeaves(below) && !IsTreeLog(below)
            && below.BlockMaterial is not (EnumBlockMaterial.Plant or EnumBlockMaterial.Snow) && !below.IsLiquid();
    }

    /// <summary>
    /// Looks down each block column of a chunk column for wild leaves standing on the ground: the foot of a bush.
    /// Leaves, logs, plants, snow and air are looked through; anything else is the ground and ends the column.
    /// </summary>
    void ScanBushes(int chunkX, int chunkZ, AreaScanner scanner)
    {
        var ba = sapi.World.BlockAccessor;
        int size = GlobalConstants.ChunkSize;
        var pos = new BlockPos(0);
        var below = new BlockPos(0);
        for (int x = chunkX * size; x < chunkX * size + size; x++)
        for (int z = chunkZ * size; z < chunkZ * size + size; z++)
        {
            if (!scanner.InRange(x, z)) continue;
            int top = ba.GetRainMapHeightAt(x, z);
            for (int y = top; y > top - TreeScanDepth && y > 1; y--)
            {
                pos.Set(x, y, z);
                var block = ba.GetBlock(pos);
                if (IsWildLeaves(block))
                {
                    below.Set(x, y - 1, z);
                    if (IsBushGround(below))
                    {
                        scanner.Found[pos.Copy()] = true;
                        break;
                    }
                    continue;
                }
                if (block.Id == 0 || IsTreeLog(block) || block.BlockMaterial is EnumBlockMaterial.Plant or EnumBlockMaterial.Snow) continue;
                break;
            }
        }
    }

    /// <summary>
    /// The leaves (and the odd log) of the bush at this spot, or null when it is not a bush: it is too big
    /// (config ForesterBushMaxBlocks) or has as many logs as a tree, which the forester fells instead.
    /// </summary>
    List<BlockPos>? BushBlocks(BlockPos start)
    {
        var ba = sapi.World.BlockAccessor;
        if (!IsWildLeaves(ba.GetBlock(start))) return null;
        int max = Math.Max(1, Config.ForesterBushMaxBlocks);
        var found = new List<BlockPos> { start.Copy() };
        var seen = new HashSet<BlockPos> { start.Copy() };
        int logs = 0;
        for (int i = 0; i < found.Count; i++)
        {
            foreach (var face in BlockFacing.ALLFACES)
            {
                var next = found[i].AddCopy(face);
                if (!seen.Add(next)) continue;
                var block = ba.GetBlock(next);
                bool log = IsTreeLog(block);
                if (!log && !IsWildLeaves(block)) continue;
                if (log && ++logs >= MinTreeLogs) return null;
                found.Add(next);
                if (found.Count > max) return null;
            }
        }
        return found;
    }

    /// <summary>
    /// A wild bush nearest the hut within the forester's range, skipping the given spots, or null. Bushes of a
    /// wood turned off on the Allowed to collect tab, and bushes in or beside a colony building, are left alone.
    /// </summary>
    public BlockPos? FindBush(Colony colony, Building hut, System.Func<BlockPos, bool> skip)
    {
        if (!Config.ForesterClearsBushes) return null;
        var ba = sapi.World.BlockAccessor;
        var scanner = Scanner(hut, "bushes", ScanBushes);
        foreach (var bush in scanner.Nearest())
        {
            bool loaded = ba.GetChunkAtBlockPos(bush) != null;
            if (loaded && (!IsWildLeaves(ba.GetBlock(bush)) || !IsBushGround(bush.DownCopy()) || BushBlocks(bush) == null))
            {
                scanner.Found.Remove(bush);
                continue;
            }
            if (loaded && !MayCollect(hut, WoodKey(ba.GetBlock(bush)))) continue;
            if (skip(bush) || !CanWorkAt(colony, bush) || NearBuilding(colony, bush)) continue;
            return bush;
        }
        return null;
    }

    /// <summary>
    /// Clears the bush at this spot with the axe in the worker's hand: every leaf comes away (giving sticks and
    /// seeds as when a player breaks them), and each log in it wears the axe down. True when anything came away.
    /// </summary>
    public bool ClearBush(Colony colony, Building hut, CitizenRecord worker, EntityAgent entity, BlockPos spot, ItemSlot axe)
    {
        var ba = sapi.World.BlockAccessor;
        if (!MayCollect(hut, WoodKey(ba.GetBlock(spot)))) return false;
        var blocks = BushBlocks(spot);
        if (blocks == null) return false;
        var sound = ba.GetBlock(spot).Sounds?.Break;
        var drops = new List<ItemStack>();
        int cleared = 0;
        // Top first, so nothing is left hanging in the air.
        foreach (var pos in blocks.OrderByDescending(p => p.Y))
        {
            var block = ba.GetBlock(pos);
            if (block.Id == 0) continue;
            bool isLog = IsTreeLog(block);
            if (isLog && axe.Empty) continue;
            drops.AddRange(DropsOf(block, pos));
            BreakQuietly(pos);
            cleared++;
            if (isLog) WearTool(entity, axe);
        }
        if (cleared == 0) return false;
        if (sound is { } breakSound) sapi.World.PlaySoundAt(breakSound, spot, 0);
        StoreHarvest(colony, hut, worker, drops, spot);
        AddStat(hut, "bushes-cleared");
        return true;
    }

    /// <summary>
    /// Plants a tree where one was felled: a seed or sapling of the same wood if the worker has one,
    /// otherwise any it has. Does nothing when the worker has none or the ground will not take it.
    /// </summary>
    public bool Replant(Colony colony, CitizenRecord worker, BlockPos spot, string? wood)
    {
        var ba = sapi.World.BlockAccessor;
        if (ba.GetBlock(spot).Id != 0 || ba.GetBlock(spot.DownCopy()).Fertility <= 0) return false;

        bool SameWood(ItemStack s) => IsTreeSeed(s) && wood != null && (VariantOf(s.Collectible, "type") == wood || VariantOf(s.Collectible, "wood") == wood);
        var seed = TakeFromPack(colony, worker, SameWood, 1) ?? TakeFromPack(colony, worker, IsTreeSeed, 1);
        if (seed == null) return false;

        // A tree seed grows into the sapling of its type; a sapling is planted as it is.
        Block? sapling = seed.Collectible is ItemTreeSeed
            ? sapi.World.GetBlock(new AssetLocation(seed.Collectible.Code.Domain, "sapling-" + VariantOf(seed.Collectible, "type") + "-free"))
            : seed.Block;
        string failure = "";
        var sel = new BlockSelection { Position = spot.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 0, 0.5) };
        if (sapling == null || !sapling.TryPlaceBlock(sapi.World, null, seed, sel, ref failure))
        {
            AddToPack(colony, worker, seed);
            return false;
        }
        sapi.World.PlaySoundAt(new AssetLocation("sounds/block/dirt1"), spot, 0);
        return true;
    }
}
