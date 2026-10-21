using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// Searches a large circle around a hut one chunk column (32 by 32 blocks) at a time, nearest first,
/// so a worker can look hundreds of blocks out without stalling the server. What it finds is kept,
/// with whether it was ready to work on when seen, and is checked again before use. Columns that were
/// not loaded are set aside for the colony to load and look at. When a sweep is done it starts over
/// after a while, to pick up new growth.
/// </summary>
public class AreaScanner
{
    /// <summary>Looks at one chunk column and records what it finds with Found.</summary>
    public delegate void ColumnScan(int chunkX, int chunkZ, AreaScanner scanner);

    public readonly BlockPos Center;
    public readonly int Radius;
    /// <summary>Positions found, and whether each was ready (a ripe bush, say) when last seen.</summary>
    public readonly Dictionary<BlockPos, bool> Found = new();
    /// <summary>Chunk columns that were not loaded when the sweep reached them, nearest first.</summary>
    public readonly List<Vec2i> Unseen = new();
    /// <summary>When the last sweep finished, in world milliseconds, or -1 while sweeping.</summary>
    public long SweepDoneMs = -1;
    /// <summary>When a worker last asked for this scanner, so scanners for removed huts can be dropped.</summary>
    public long LastUsedMs;

    readonly List<Vec2i> columns;
    readonly ColumnScan scan;
    int cursor;

    public AreaScanner(BlockPos center, int radius, ColumnScan scan)
    {
        Center = center.Copy();
        Radius = radius;
        this.scan = scan;
        int size = GlobalConstants.ChunkSize;
        int cx0 = (center.X - radius) / size, cx1 = (center.X + radius) / size;
        int cz0 = (center.Z - radius) / size, cz1 = (center.Z + radius) / size;
        columns = new List<Vec2i>();
        for (int cx = cx0; cx <= cx1; cx++)
        for (int cz = cz0; cz <= cz1; cz++)
        {
            if (ColumnDistanceSq(cx, cz) <= (double)radius * radius) columns.Add(new Vec2i(cx, cz));
        }
        columns.Sort((a, b) => ColumnDistanceSq(a.X, a.Y).CompareTo(ColumnDistanceSq(b.X, b.Y)));
    }

    /// <summary>Squared distance from the hut to the nearest point of a chunk column.</summary>
    double ColumnDistanceSq(int cx, int cz)
    {
        int size = GlobalConstants.ChunkSize;
        double dx = Math.Max(0, Math.Max(cx * size - Center.X, Center.X - (cx * size + size - 1)));
        double dz = Math.Max(0, Math.Max(cz * size - Center.Z, Center.Z - (cz * size + size - 1)));
        return dx * dx + dz * dz;
    }

    public bool InRange(int x, int z)
    {
        long dx = x - Center.X, dz = z - Center.Z;
        return dx * dx + dz * dz <= (long)Radius * Radius;
    }

    public bool Sweeping => cursor < columns.Count;

    /// <summary>Looks at the next column of the sweep. False when the sweep is already done.</summary>
    public bool Step(IBlockAccessor ba)
    {
        if (cursor >= columns.Count) return false;
        var column = columns[cursor++];
        if (ba.GetMapChunk(column.X, column.Y) == null)
        {
            if (!Unseen.Contains(column)) Unseen.Add(column);
        }
        else
        {
            scan(column.X, column.Y, this);
        }
        return true;
    }

    /// <summary>Looks at a column that has just been loaded.</summary>
    public void ScanLoaded(Vec2i column)
    {
        Unseen.Remove(column);
        scan(column.X, column.Y, this);
    }

    public void Restart()
    {
        cursor = 0;
        SweepDoneMs = -1;
    }

    /// <summary>Found positions, nearest the hut first.</summary>
    public List<BlockPos> Nearest() =>
        Found.Keys.OrderBy(p => p.DistanceSqTo(Center.X, Center.Y, Center.Z)).ToList();
}
