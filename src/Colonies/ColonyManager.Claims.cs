using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Colonies;

/// <summary>
/// The colony's land claim grows with the colony, as in MineColonies. The Town Hall claims a square around
/// itself, and every finished hut adds a square around its hut block; the claim is all of those squares
/// together, kept as the areas of the colony's one vanilla land claim. So everything that asks "is this
/// the colony's land?" (placing huts, field markers, warehouses, what builders may touch, which beds and
/// chests belong to the colony) sees the grown claim without knowing about huts.
/// </summary>
public partial class ColonyManager
{
    /// <summary>The square each building claims, by building id, after the last refresh; 0 when blocked. Not saved.</summary>
    readonly Dictionary<string, Dictionary<int, int>> claimedSizes = new();

    /// <summary>How many blocks along each side a building of this type claims (before shrinking around other claims).</summary>
    public int ClaimSize(string type)
    {
        int min = Math.Max(1, Config.ClaimSizeMin);
        int max = Math.Max(min, Config.ClaimSizeMax);
        int size = type == BuildingTypes.TownHall ? Config.TownHallClaimSize
            : Config.HutClaimSizes.TryGetValue(type, out int listed) ? listed
            : IsMilitary(type) ? Config.MilitaryClaimSize
            : min;
        return Math.Clamp(size, min, max);
    }

    static bool IsMilitary(string type) =>
        BuildingCategories.All.Any(c => c.Code == "military" && Array.IndexOf(c.Types, type) >= 0);

    /// <summary>
    /// A building claims land once it is built: the Town Hall at once, a hut when its first level is done, and a
    /// building with nothing to build (no blueprints, like the Warehouse) as soon as it is placed.
    /// </summary>
    bool ClaimsLand(Building building) =>
        building.Type == BuildingTypes.TownHall || building.Level >= 1 || Blueprints.MaxLevel(building) == 0;

    /// <summary>A size by size square centred on a block, from ClaimDepthBelow under it to ClaimHeightAbove over it.</summary>
    Cuboidi ClaimArea(BlockPos center, int size)
    {
        int x1 = center.X - size / 2, z1 = center.Z - size / 2;
        return new Cuboidi(
            x1, Math.Max(0, center.Y - Config.ClaimDepthBelow), z1,
            x1 + size, center.Y + Config.ClaimHeightAbove + 1, z1 + size
        );
    }

    /// <summary>
    /// The biggest square up to the building's claim size that overlaps no one else's land claim, shrinking two
    /// blocks at a time, or null if not even ClaimSizeMin fits.
    /// </summary>
    Cuboidi? FitClaim(BlockPos center, int size, string? ownDescription)
    {
        var others = sapi.World.Claims.All.Where(c => ownDescription == null || c.Description != ownDescription).ToList();
        int min = Math.Min(size, Math.Max(1, Config.ClaimSizeMin));
        for (int s = size; ; s = Math.Max(min, s - 2))
        {
            var area = ClaimArea(center, s);
            if (!others.Any(c => c.Intersects(area))) return area;
            if (s == min) return null;
        }
    }

    /// <summary>
    /// Brings the colony's land claim in line with its buildings: one square for the Town Hall and each finished
    /// hut. Called every colony tick and whenever a building is finished or removed; does nothing if nothing changed.
    /// </summary>
    public void RefreshClaim(Colony colony)
    {
        var claim = FindClaim(colony);
        if (claim == null) return;

        string mine = ClaimDescription(colony);
        var sizes = new Dictionary<int, int>();
        var areas = new List<Cuboidi>();
        // The Town Hall first, so its square is always there; then the oldest huts first.
        foreach (var building in colony.Buildings.OrderBy(b => b.Type == BuildingTypes.TownHall ? 0 : 1).ThenBy(b => b.BuildingId))
        {
            if (!ClaimsLand(building)) continue;
            var area = FitClaim(building.HutPos, ClaimSize(building.Type), mine);
            sizes[building.BuildingId] = area == null ? 0 : area.SizeX;
            if (area != null && !areas.Any(a => a.Equals(area))) areas.Add(area);
        }
        claimedSizes[colony.ColonyId] = sizes;
        if (areas.Count == 0 || areas.SequenceEqual(claim.Areas)) return;

        // Claims are replaced rather than edited so the server re-syncs them to clients.
        sapi.World.Claims.Remove(claim);
        claim.Areas = areas;
        sapi.World.Claims.Add(claim);
        colony.ClaimRadius = ClaimSize(BuildingTypes.TownHall) / 2;
        MarkDirty(colony);
    }

    /// <summary>The square this building claims, in blocks along a side; 0 if it claims nothing yet or is blocked.</summary>
    public int ClaimedSize(Colony colony, Building building)
    {
        if (!claimedSizes.ContainsKey(colony.ColonyId)) RefreshClaim(colony);
        return claimedSizes.GetValueOrDefault(colony.ColonyId)?.GetValueOrDefault(building.BuildingId) ?? 0;
    }

    /// <summary>The areas of the colony's land claim.</summary>
    public IReadOnlyList<Cuboidi> ClaimAreas(Colony colony) => FindClaim(colony)?.Areas ?? new List<Cuboidi>();

    /// <summary>True when the block is on the colony's land.</summary>
    public bool InClaim(Colony colony, BlockPos pos) => FindClaim(colony)?.PositionInside(pos) == true;

    /// <summary>How many columns of land (blocks seen from above) the colony's claim covers, counting overlaps once.</summary>
    public long ClaimedColumns(Colony colony)
    {
        var areas = ClaimAreas(colony);
        var xs = areas.SelectMany(a => new[] { a.X1, a.X2 }).Distinct().OrderBy(x => x).ToList();
        long total = 0;
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            // In each strip between two edges, add up the lengths of the squares crossing it, merged where they overlap.
            int x = xs[i];
            var spans = areas.Where(a => a.X1 <= x && a.X2 > x).Select(a => (a.Z1, a.Z2)).OrderBy(s => s.Z1).ToList();
            long length = 0;
            int end = int.MinValue;
            foreach (var (z1, z2) in spans)
            {
                int from = Math.Max(z1, end);
                if (z2 > from) length += z2 - from;
                end = Math.Max(end, z2);
            }
            total += length * (xs[i + 1] - x);
        }
        return total;
    }

    /// <summary>
    /// The ground along the edge of the claim, for showing it: for each square, the columns on its border that
    /// lie next to land outside every square. Only loaded columns; at most max of them.
    /// </summary>
    public List<BlockPos> ClaimBorder(Colony colony, int max)
    {
        var areas = ClaimAreas(colony);
        var ba = sapi.World.BlockAccessor;
        var seen = new HashSet<(int, int)>();
        var border = new List<BlockPos>();
        bool Inside(int x, int z) => areas.Any(a => a.Contains(x, z));

        void Try(int x, int z, int outX, int outZ)
        {
            if (border.Count >= max || Inside(outX, outZ) || !seen.Add((x, z))) return;
            if (ba.GetChunkAtBlockPos(x, 1, z) == null) return;
            var pos = new BlockPos(x, 0, z);
            pos.Y = ba.GetTerrainMapheightAt(pos) + 1;
            border.Add(pos);
        }

        foreach (var a in areas)
        {
            for (int x = a.X1; x < a.X2; x++)
            {
                Try(x, a.Z1, x, a.Z1 - 1);
                Try(x, a.Z2 - 1, x, a.Z2);
            }
            for (int z = a.Z1; z < a.Z2; z++)
            {
                Try(a.X1, z, a.X1 - 1, z);
                Try(a.X2 - 1, z, a.X2, z);
            }
        }
        return border;
    }

    /// <summary>One line per building that claims land, for the claims command.</summary>
    public List<string> ClaimLines(Colony colony)
    {
        RefreshClaim(colony);
        var lines = new List<string>();
        foreach (var building in colony.Buildings.OrderBy(b => b.BuildingId))
        {
            string name = Lang.Get("createacolony:building-" + building.Type);
            string where = building.HutPos.ToLocalPosition(sapi).ToString();
            int size = ClaimedSize(colony, building);
            lines.Add(!ClaimsLand(building) ? Lang.Get("createacolony:claims-line-unbuilt", name, where, ClaimSize(building.Type))
                : size == 0 ? Lang.Get("createacolony:claims-line-blocked", name, where)
                : size < ClaimSize(building.Type) ? Lang.Get("createacolony:claims-line-shrunk", name, where, size, ClaimSize(building.Type))
                : Lang.Get("createacolony:claims-line", name, where, size));
        }
        return lines;
    }
}
