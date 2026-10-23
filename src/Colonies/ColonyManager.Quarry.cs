using System;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// The quarrier's trade. A Quarry marker marks the middle of a square pit; the quarrier digs it out a
/// layer at a time from the top, for the stone. A staircase is left standing around the pit's edge
/// (each block of the edge one step lower than the last, going round clockwise from the north-west
/// corner), so the quarrier can always walk down to the bottom and back out.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Blocks from the pit's middle to its edge.</summary>
    int QuarryRadius => (Math.Max(3, Config.QuarrySize) - 1) / 2;

    /// <summary>How many layers the pit goes down; the staircase round the edge sets the limit.</summary>
    int QuarryLayers => Math.Clamp(Config.QuarryDepth, 1, 8 * QuarryRadius - 1);

    /// <summary>
    /// Where a cell of the pit's edge is along the staircase, from 0 at the north-west corner going east,
    /// then south, west and north again; -1 for a cell inside the edge.
    /// </summary>
    public static int StairIndex(int i, int j, int r)
    {
        if (j == -r) return i + r;
        if (i == r) return 2 * r + (j + r);
        if (j == r) return 4 * r + (r - i);
        if (i == -r) return 6 * r + (r - j);
        return -1;
    }

    /// <summary>How many layers are dug out at a cell: all of them inside, fewer along the staircase.</summary>
    public static int CellDepth(int i, int j, int r, int layers)
    {
        int stair = StairIndex(i, j, r);
        return stair < 0 ? layers : Math.Min(stair, layers);
    }

    /// <summary>The nearest Quarry within marker range of a spot, or null.</summary>
    public Building? QuarryFor(Colony colony, BlockPos marker) => colony.Buildings
        .Where(b => b.Type == BuildingTypes.Quarry && b.HutPos.HorDistanceSqTo(marker.X, marker.Z) <= (double)GatherRadius(b) * GatherRadius(b))
        .OrderBy(b => b.HutPos.HorDistanceSqTo(marker.X, marker.Z))
        .FirstOrDefault();

    /// <summary>Sets a Quarry's pit around a newly placed marker.</summary>
    public void SetQuarry(Colony colony, Building quarry, BlockPos marker)
    {
        quarry.QuarryCenter = marker.Copy();
        MarkDirty(colony);
    }

    /// <summary>
    /// The next block of the pit to dig: the highest layer not yet dug out, nearest the worker. Blocks that
    /// cannot be dug (water beside them, a building, someone else's land) are left as they are, and so is
    /// whatever lies under them. Null when the pit is finished, with "done" set.
    /// </summary>
    public BlockPos? NextQuarryBlock(Colony colony, Building hut, Vec3d near, System.Func<BlockPos, bool> skip, out bool done)
    {
        done = false;
        if (hut.QuarryCenter is not { } center) return null;
        var ba = sapi.World.BlockAccessor;
        if (ba.GetChunkAtBlockPos(center) == null) return center; // walk there first
        var boxes = DigProtected(colony);
        int r = QuarryRadius, layers = QuarryLayers, top = center.Y - 1;
        bool anyLeft = false;
        for (int layer = -Math.Max(0, Config.QuarryClearAbove); layer < layers; layer++)
        {
            BlockPos? best = null;
            double bestDistance = double.MaxValue;
            for (int i = -r; i <= r; i++)
            for (int j = -r; j <= r; j++)
            {
                if (layer >= 0 && layer >= CellDepth(i, j, r, layers)) continue;
                var pos = new BlockPos(center.X + i, top - layer, center.Z + j);
                if (IsClear(pos)) continue;
                // Under a block left standing, the column stays as it is.
                if (!IsClear(pos.UpCopy()) && layer > -Config.QuarryClearAbove) continue;
                if (!CanDig(colony, pos, boxes)) continue;
                anyLeft = true;
                if (skip(pos)) continue;
                double d = pos.ToVec3d().Add(0.5, 1, 0.5).SquareDistanceTo(near);
                if (d < bestDistance) (best, bestDistance) = (pos, d);
            }
            if (best != null) return best;
            // Blocks of this layer that are out of reach for now hold up the layers below.
            if (anyLeft) return null;
        }
        done = true;
        return null;
    }

    /// <summary>The Quarry window line: where the pit is, how big, and how far down it has got.</summary>
    public string QuarryInfo(Building hut)
    {
        if (hut.QuarryCenter is not { } center) return Lang.Get("createacolony:quarry-no-pit", GatherRadius(hut));
        int size = QuarryRadius * 2 + 1;
        return Lang.Get("createacolony:quarry-pit", size, QuarryLayers, center.X, center.Y, center.Z);
    }
}
