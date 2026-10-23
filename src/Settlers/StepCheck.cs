using System;
using CreateAColony.Colonies;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Essentials;

namespace CreateAColony.Settlers;

/// <summary>
/// Keeps the game's pathfinder from planning steps a settler cannot climb. When it checks a step up onto a
/// block, it only looks for head room on top and never compares the height of the step with how high the
/// walker can step. So it plans paths over fences, walls and other blocks taller than a full block (1.5
/// blocks for a fence), and the settler then walks into them and stays there. While it plans for a settler
/// (DoorPathing knows when), a step up that rises more than the settler's step height is refused.
///
/// It also never checks head room where the step starts: it checks that the settler fits standing on top of
/// the block, but not that it can rise there from where it stands. A block hanging at head height over the
/// spot before a step (a low roof edge, a floating block, a beam) stops the climb, and the settler walked into
/// it over and over. A step up is refused unless the settler's whole body fits at the new height over the
/// spot it starts from and halfway across.
///
/// Drops are limited too: a path may walk down at most MaxDropHeight blocks (2) at a time, where the game allows 4.
/// </summary>
public static class StepCheck
{
    /// <summary>A little room for blocks whose top is a hair above a whole number.</summary>
    const double Tolerance = 0.05;

    /// <summary>Config MaxDropHeight: deeper drops are refused. Set once on the main thread when the server starts.</summary>
    static int maxDrop = 2;

    [ThreadStatic] static BlockPos? tmpPos;
    [ThreadStatic] static Vec3d? tmpVec;
    [ThreadStatic] static CollisionTester? collisionTester;

    public static void Init(ColonyConfig config) => maxDrop = Math.Max(1, config.MaxDropHeight);

    public static void Patch(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(AStar), "traversable"),
            prefix: new HarmonyMethod(typeof(StepCheck), nameof(Before)),
            postfix: new HarmonyMethod(typeof(StepCheck), nameof(After)));
    }

    /// <summary>Remembers the height the pathfinder starts the step at; it moves node up or down while it checks.</summary>
    static void Before(PathNode node, out int __state) => __state = node.Y;

    static void After(AStar __instance, PathNode node, float stepHeight, Cuboidf entityCollBox, Cardinal fromDir, int __state, ref bool __result)
    {
        if (!__result || !DoorPathing.Planning) return;
        // node only ends up lower when the pathfinder walked off an edge; it goes down by the blocks fallen.
        // The game lets creatures drop 4 blocks, deep enough that settlers walked into holes and pits.
        if (__state - node.Y > maxDrop)
        {
            __result = false;
            return;
        }
        // node only ends up higher when the pathfinder stepped up onto the block in front.
        if (node.Y <= __state || PathBlocks.blockAccess == null) return;
        var ba = PathBlocks.blockAccess(__instance);
        if (ba == null) return;
        var pos = tmpPos ??= new BlockPos(0);
        pos.dimension = node.dimension;

        double top = Top(ba, pos.Set(node.X, __state, node.Z));
        if (top <= 0) return;
        double from = Floor(ba, pos, node.X - fromDir.Normali.X, __state, node.Z - fromDir.Normali.Z);
        if (__state + top - from > stepHeight + Tolerance)
        {
            __result = false;
            return;
        }

        // Head room for the climb: the body at the new height, over the spot it starts from and halfway across.
        var tester = collisionTester ??= new CollisionTester();
        var at = tmpVec ??= new Vec3d();
        double x = node.X - fromDir.Normali.X + __instance.centerOffsetX, z = node.Z - fromDir.Normali.Z + __instance.centerOffsetZ;
        double y = __state + top + Tolerance;
        if (tester.IsColliding(ba, entityCollBox, at.Set(x, y, z), false)
            || tester.IsColliding(ba, entityCollBox, at.Set(x + fromDir.Normali.X * 0.5, y, z + fromDir.Normali.Z * 0.5), false))
        {
            __result = false;
        }
    }

    /// <summary>
    /// Where the feet are when standing in the cell at x, y, z: on a low block in the cell (a slab, a path), else
    /// on the block below. Unknown floors (water) count as the bottom of the cell, which lets the step through.
    /// </summary>
    static double Floor(IBlockAccessor ba, BlockPos pos, int x, int y, int z)
    {
        double inCell = Top(ba, pos.Set(x, y, z));
        if (inCell > 0 && inCell < 1) return y + inCell;
        double below = Top(ba, pos.Set(x, y - 1, z));
        return below > 0 ? y - 1 + below : y;
    }

    /// <summary>The top of the block's collision boxes, relative to its own cell; 0 when it has none.</summary>
    static double Top(IBlockAccessor ba, BlockPos pos)
    {
        var boxes = ba.GetBlock(pos, BlockLayersAccess.Solid).GetCollisionBoxes(ba, pos);
        if (boxes == null) return 0;
        double top = 0;
        foreach (var box in boxes)
        {
            if (box != null) top = Math.Max(top, box.Y2);
        }
        return top;
    }
}
