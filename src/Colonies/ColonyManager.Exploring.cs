using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// The Explorer's and Gatherer's trades. The explorer walks out over the land around its hut, one square at a
/// time, nearest first, and marks what it sees on the colony's map: ore lying on the ground, clay, traders,
/// animals, danger and landmarks. The gatherer forages near its hut (ripe berries and fruit, edible mushrooms,
/// loose stones, flints and sticks) and marks the plants it passes. Markers: ColonyManager.Markers.cs.
/// </summary>
public partial class ColonyManager
{
    // ---- Explorer ----

    /// <summary>The square of land (ExplorerStep wide) a spot is in, as one number for Building.Explored.</summary>
    public long ExploreCell(int x, int z)
    {
        int step = Math.Max(8, Config.ExplorerStep);
        return ((long)(int)Math.Floor(x / (double)step) << 32) | (uint)(int)Math.Floor(z / (double)step);
    }

    /// <summary>
    /// The middle of the nearest square of land the explorer has not looked over yet, within its hut's range, or
    /// null when it has seen it all. Squares it could not reach (skip) count as seen. Once everything is seen it
    /// starts over after ExplorerRevisitDays.
    /// </summary>
    public BlockPos? NextExploreSpot(Colony colony, Building hut, Vec3d from, System.Func<BlockPos, bool> skip)
    {
        var ba = sapi.World.BlockAccessor;
        int radius = Math.Max(1, GatherRadius(hut)), step = Math.Max(8, Config.ExplorerStep);
        var seen = new HashSet<long>(hut.Explored);
        double now = sapi.World.Calendar.TotalDays;
        if (hut.ExploreStartedTotalDays <= 0) hut.ExploreStartedTotalDays = now;

        int c0x = (int)Math.Floor((hut.HutPos.X - radius) / (double)step), c1x = (int)Math.Floor((hut.HutPos.X + radius) / (double)step);
        int c0z = (int)Math.Floor((hut.HutPos.Z - radius) / (double)step), c1z = (int)Math.Floor((hut.HutPos.Z + radius) / (double)step);
        BlockPos? best = null;
        double bestDist = double.MaxValue;
        var rand = sapi.World.Rand;
        for (int cx = c0x; cx <= c1x; cx++)
        for (int cz = c0z; cz <= c1z; cz++)
        {
            int x = cx * step + step / 2, z = cz * step + step / 2;
            double hx = x - hut.HutPos.X, hz = z - hut.HutPos.Z;
            if (hx * hx + hz * hz > (double)radius * radius) continue;
            if (seen.Contains(ExploreCell(x, z))) continue;
            // Mostly the nearest square to where the explorer stands, with a little wander in it.
            double dx = x - from.X, dz = z - from.Z;
            double dist = dx * dx + dz * dz + rand.NextDouble() * step * step;
            if (dist >= bestDist) continue;
            var pos = new BlockPos(x, hut.HutPos.Y + 1, z);
            if (ba.GetChunkAtBlockPos(pos) != null)
            {
                pos.Y = ba.GetTerrainMapheightAt(pos) + 1;
                // A lake or the sea is looked over from its shore, from the squares around it.
                if (ba.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid).Id != 0 || !CanWorkAt(colony, pos))
                {
                    hut.Explored.Add(ExploreCell(x, z));
                    seen.Add(ExploreCell(x, z));
                    continue;
                }
            }
            if (skip(pos))
            {
                hut.Explored.Add(ExploreCell(x, z));
                seen.Add(ExploreCell(x, z));
                continue;
            }
            best = pos;
            bestDist = dist;
        }

        if (best == null && hut.Explored.Count > 0 && now - hut.ExploreStartedTotalDays >= Config.ExplorerRevisitDays)
        {
            // Traders and animals move on: go round again.
            hut.Explored.Clear();
            hut.ExploreStartedTotalDays = now;
            MarkDirty(colony);
        }
        return best;
    }

    /// <summary>The explorer looks around where it stands, marks what it sees, and counts the square as seen.</summary>
    public void Explore(Colony colony, Building hut, BlockPos spot)
    {
        LookAround(colony, hut, spot, Math.Max(4, Config.ExplorerLookRadius));
        long cell = ExploreCell(spot.X, spot.Z);
        if (!hut.Explored.Contains(cell)) hut.Explored.Add(cell);
        AddStat(hut, "explored");
        MarkDirty(colony);
    }

    /// <summary>About how much of the explorer's range it has looked over, from 0 to 100.</summary>
    public int ExploredPercent(Building hut)
    {
        int radius = Math.Max(1, GatherRadius(hut)), step = Math.Max(8, Config.ExplorerStep);
        var seen = new HashSet<long>(hut.Explored);
        int all = 0, done = 0;
        int c0x = (int)Math.Floor((hut.HutPos.X - radius) / (double)step), c1x = (int)Math.Floor((hut.HutPos.X + radius) / (double)step);
        int c0z = (int)Math.Floor((hut.HutPos.Z - radius) / (double)step), c1z = (int)Math.Floor((hut.HutPos.Z + radius) / (double)step);
        for (int cx = c0x; cx <= c1x; cx++)
        for (int cz = c0z; cz <= c1z; cz++)
        {
            int x = cx * step + step / 2, z = cz * step + step / 2;
            double hx = x - hut.HutPos.X, hz = z - hut.HutPos.Z;
            if (hx * hx + hz * hz > (double)radius * radius) continue;
            all++;
            if (seen.Contains(ExploreCell(x, z))) done++;
        }
        return all == 0 ? 100 : done * 100 / all;
    }

    /// <summary>The Explorer's Hut window line: how much it has seen and how many markers it put down.</summary>
    string ExplorerInfo(Colony colony, Building hut) =>
        Lang.Get("createacolony:explorer-progress", ExploredPercent(hut), MarkerCount(colony, hut));

    // ---- Gatherer ----

    /// <summary>When each Gatherer's Hut's gatherer last went for a look around, so it does not wander all the time.</summary>
    readonly Dictionary<int, long> lastForageWalk = new();

    /// <summary>What a gatherer could take from a block: "fruit", "mushroom", "loose", or null for nothing.</summary>
    string? Forage(Colony colony, BlockPos pos)
    {
        var ba = sapi.World.BlockAccessor;
        var be = ba.GetBlockEntity(pos);
        if (IsFruitBearer(be)) return IsRipeFruit(be) ? "fruit" : null;
        var block = ba.GetBlock(pos);
        string path = block.Code?.Path ?? "";
        if (path.StartsWith("mushroom-")) return IsPoisonousMushroom(block) ? null : "mushroom";
        if (Config.GathererPicksUpStones && (path.StartsWith("loosestones-") || path.StartsWith("looseflints-") || path.StartsWith("loosestick")))
        {
            // On open land or the colony's own, but not in or beside a building, so stones placed on purpose stay.
            return CanWorkAt(colony, pos) && !NearBuilding(colony, pos) ? "loose" : null;
        }
        return null;
    }

    /// <summary>Each colony's building footprints, two blocks wider all round, and when they were worked out.</summary>
    readonly Dictionary<string, (long Ms, List<Cuboidi> Boxes)> buildingBoxes = new();

    /// <summary>True when a spot is in a colony building or within two blocks of one.</summary>
    bool NearBuilding(Colony colony, BlockPos pos)
    {
        long now = sapi.World.ElapsedMilliseconds;
        if (!buildingBoxes.TryGetValue(colony.ColonyId, out var cached) || now - cached.Ms > 30_000)
        {
            var boxes = colony.Buildings.Select(b =>
            {
                var box = Footprint(b);
                return new Cuboidi(box.X1 - 2, box.Y1 - 2, box.Z1 - 2, box.X2 + 3, box.Y2 + 3, box.Z2 + 3);
            }).ToList();
            buildingBoxes[colony.ColonyId] = cached = (now, boxes);
        }
        return cached.Boxes.Any(b => b.Contains(pos.X, pos.Y, pos.Z));
    }

    /// <summary>Notes every berry bush, fruit tree branch, mushroom and loose stone in a chunk column.</summary>
    void ScanForage(int chunkX, int chunkZ, AreaScanner scanner)
    {
        var ba = sapi.World.BlockAccessor;
        int size = GlobalConstants.ChunkSize;
        for (int cy = 0; cy < ba.MapSizeY / size; cy++)
        {
            var chunk = ba.GetChunk(chunkX, cy, chunkZ);
            if (chunk?.BlockEntities == null) continue;
            foreach (var be in chunk.BlockEntities.Values)
            {
                if (IsFruitBearer(be) && scanner.InRange(be.Pos.X, be.Pos.Z)) scanner.Found[be.Pos.Copy()] = IsRipeFruit(be);
            }
        }
        var pos = new BlockPos(0);
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            int wx = chunkX * size + x, wz = chunkZ * size + z;
            if (!scanner.InRange(wx, wz)) continue;
            pos.Set(wx, 0, wz);
            int top = ba.GetTerrainMapheightAt(pos);
            for (int y = top; y <= top + 1; y++)
            {
                pos.Y = y;
                string path = ba.GetBlock(pos).Code?.Path ?? "";
                if (path.StartsWith("mushroom-") || path.StartsWith("loosestones-") || path.StartsWith("looseflints-") || path.StartsWith("loosestick"))
                {
                    scanner.Found[pos.Copy()] = true;
                }
            }
        }
    }

    /// <summary>The nearest thing the gatherer can take right now, or null.</summary>
    public BlockPos? NextForage(Colony colony, Building hut, Vec3d from, System.Func<BlockPos, bool> skip)
    {
        var ba = sapi.World.BlockAccessor;
        var scanner = Scanner(hut, "forage", ScanForage);
        BlockPos? best = null;
        double bestDist = double.MaxValue;
        foreach (var (pos, ready) in scanner.Found.ToList())
        {
            if (ba.GetChunkAtBlockPos(pos) != null)
            {
                string? kind = Forage(colony, pos);
                if (kind == null && !IsFruitBearer(ba.GetBlockEntity(pos)))
                {
                    scanner.Found.Remove(pos); // picked, broken, or never worth it
                    continue;
                }
                scanner.Found[pos] = kind != null;
                if (kind == null || !MayCollect(hut, ForageKey(pos))) continue;
            }
            else if (!ready) continue;
            if (skip(pos) || !CanWorkAt(colony, pos)) continue;
            double dist = pos.DistanceSqTo(from.X, from.Y, from.Z);
            if (dist >= bestDist) continue;
            best = pos;
            bestDist = dist;
        }
        return best;
    }

    /// <summary>
    /// A spot in the gatherer's range to walk to and look around, when there is nothing to pick; at most every
    /// two minutes, so it rests at the hut in between. Null when it is not time yet.
    /// </summary>
    public BlockPos? ForageWalk(Colony colony, Building hut, System.Func<BlockPos, bool> skip)
    {
        long now = sapi.World.ElapsedMilliseconds;
        if (lastForageWalk.TryGetValue(hut.BuildingId, out long last) && now - last < 120_000) return null;
        lastForageWalk[hut.BuildingId] = now;
        var ba = sapi.World.BlockAccessor;
        var rand = sapi.World.Rand;
        int radius = Math.Max(4, GatherRadius(hut));
        for (int tries = 0; tries < 12; tries++)
        {
            double angle = rand.NextDouble() * Math.PI * 2, dist = radius * Math.Sqrt(rand.NextDouble());
            var pos = new BlockPos((int)(hut.HutPos.X + Math.Cos(angle) * dist), hut.HutPos.Y, (int)(hut.HutPos.Z + Math.Sin(angle) * dist));
            if (ba.GetChunkAtBlockPos(pos) == null) continue;
            pos.Y = ba.GetTerrainMapheightAt(pos) + 1;
            if (ba.GetBlock(pos.DownCopy(), BlockLayersAccess.Fluid).Id != 0 || skip(pos) || !CanWorkAt(colony, pos)) continue;
            return pos;
        }
        return null;
    }

    /// <summary>Picks what is at the spot into the gatherer's inventory. False when there was nothing to take.</summary>
    public bool Gather(Colony colony, Building hut, CitizenRecord worker, BlockPos pos)
    {
        string? kind = Forage(colony, pos);
        if (kind == null || !MayCollect(hut, ForageKey(pos))) return false;
        if (kind == "fruit")
        {
            if (!PickFruit(colony, hut, worker, pos)) return false;
        }
        else
        {
            var block = sapi.World.BlockAccessor.GetBlock(pos);
            var drops = DropsOf(block, pos);
            BreakQuietly(pos);
            StoreHarvest(colony, hut, worker, drops, pos);
        }
        AddStat(hut, "gathered");
        MarkDirty(colony);
        return true;
    }

    /// <summary>The Gatherer's Hut window line: how many markers it put down.</summary>
    string GathererInfo(Colony colony, Building hut)
    {
        int ready = scanners.TryGetValue((hut.BuildingId, "forage"), out var scanner) ? scanner.Found.Count(f => f.Value) : 0;
        return Lang.Get("createacolony:gatherer-progress", MarkerCount(colony, hut), ready);
    }
}
