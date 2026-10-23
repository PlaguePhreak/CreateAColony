using System;
using System.Linq;
using CreateAColony.Colonies;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Essentials;

namespace CreateAColony.Settlers;

/// <summary>
/// Pathblocks: the blocks settlers treat as roads (config PathBlocks). A settler standing on one walks
/// PathSpeedMultiplier times faster (EntitySettler.GetWalkSpeedMultiplier), and while the game's pathfinder
/// plans for a settler, a step onto one costs PathStepCost of a normal step, so routes bend towards roads
/// when the road is not much longer. It is a soft preference: a road far out of the way is still ignored.
/// </summary>
public static class PathBlocks
{
    /// <summary>
    /// One flag per block id, filled once on the main thread when the server starts. The pathfinder reads it
    /// from its own thread, which is safe because it is never changed after that.
    /// </summary>
    static bool[] isPath = Array.Empty<bool>();
    static float stepDiscount;

    public static float SpeedMultiplier { get; private set; } = 1f;

    public static void Init(IWorldAccessor world, ColonyConfig config)
    {
        var patterns = (config.PathBlocks ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new AssetLocation(p.Contains(':') ? p : "game:" + p))
            .ToArray();
        var flags = new bool[world.Blocks.Count + 1];
        foreach (var block in world.Blocks)
        {
            if (block?.Code == null || block.Id <= 0 || block.Id >= flags.Length) continue;
            flags[block.Id] = patterns.Any(p => WildcardUtil.Match(p, block.Code));
        }
        isPath = flags;
        SpeedMultiplier = Math.Max(0.1f, config.PathSpeedMultiplier);
        // A step's cost never drops below 0.3, or the search would wander along every road it sees.
        stepDiscount = 1f - GameMath.Clamp(config.PathStepCost, 0.3f, 1f);
    }

    public static bool IsPath(int blockId) => blockId > 0 && blockId < isPath.Length && isPath[blockId];

    public static bool IsPath(Block? block) => block != null && IsPath(block.Id);

    /// <summary>
    /// True when the entity stands on a Pathblock, or in one that is lower than a full block (slabs, stairs,
    /// stone path, which is a little lower than a full block).
    /// </summary>
    public static bool StandsOnPath(Entity entity)
    {
        if (isPath.Length == 0) return false;
        var ba = entity.World.BlockAccessor;
        var pos = entity.Pos;
        int x = (int)Math.Floor(pos.X), z = (int)Math.Floor(pos.Z);
        return IsPath(ba.GetBlockRaw(x, (int)(pos.InternalY - 0.05), z).Id) || IsPath(ba.GetBlockRaw(x, (int)(pos.InternalY + 0.01), z).Id);
    }

    // The pathfinder's own block reader, which it fills as it searches. StepCheck reads blocks with it too.
    internal static AccessTools.FieldRef<AStar, ICachingBlockAccessor>? blockAccess;
    [ThreadStatic] static BlockPos? tmpPos;

    public static void Patch(Harmony harmony)
    {
        blockAccess = AccessTools.FieldRefAccess<AStar, ICachingBlockAccessor>("blockAccess");
        harmony.Patch(AccessTools.Method(typeof(AStar), "traversable"),
            postfix: new HarmonyMethod(typeof(PathBlocks), nameof(Traversable)));
    }

    /// <summary>
    /// Runs after the pathfinder decided a step can be taken. When it lands on a Pathblock, the step is made
    /// cheaper. Only for settlers (DoorPathing knows when the search is for one).
    /// </summary>
    static void Traversable(AStar __instance, PathNode node, Cardinal fromDir, ref float extraCost, bool __result)
    {
        if (!__result || stepDiscount <= 0 || !DoorPathing.Planning || blockAccess == null) return;
        var ba = blockAccess(__instance);
        if (ba == null) return;
        var pos = tmpPos ??= new BlockPos(0);
        pos.dimension = node.dimension;
        // node is where the feet go: a road a little lower than a full block is at the feet, a full one under them.
        pos.Set(node.X, node.Y, node.Z);
        bool road = IsPath(ba.GetBlock(pos, BlockLayersAccess.Solid).Id);
        if (!road)
        {
            pos.Y--;
            road = IsPath(ba.GetBlock(pos, BlockLayersAccess.Solid).Id);
        }
        if (road) extraCost -= stepDiscount * (fromDir.IsDiagonal ? 1.4142136f : 1f);
    }
}
