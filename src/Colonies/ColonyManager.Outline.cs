using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// The outline of a building for the placement preview: the outer shell of a blueprint's blocks where
/// the building would stand, blue where the spot is free and red where something stands that the
/// builder would have to clear first.
/// </summary>
public partial class ColonyManager
{
    static readonly int OutlineFree = ColorUtil.ColorFromRgba(90, 170, 255, 80);
    static readonly int OutlineBlocked = ColorUtil.ColorFromRgba(255, 70, 60, 130);
    static readonly int OutlineNearby = ColorUtil.ColorFromRgba(190, 110, 255, 140);
    static readonly int OutlineNearbyTop = ColorUtil.ColorFromRgba(190, 110, 255, 55);
    static readonly int OutlineOverlap = ColorUtil.ColorFromRgba(255, 150, 30, 180);
    static readonly (int, int, int)[] Neighbours = { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };

    /// <summary>
    /// The outline blocks with their colours, and how many of them are in the way. At most max blocks. With
    /// blockedOnly (beside a ghost preview) only the blocks in the way are listed, inside the walls too.
    /// </summary>
    public (List<BlockPos> Positions, List<int> Colors, int Blocked, int Total) Outline(Building building, Blueprint blueprint, int max, bool blockedOnly = false)
    {
        var ba = sapi.World.BlockAccessor;
        var solid = blueprint.Steps.Where(s => s.BlockId != 0).ToList();
        var filled = new HashSet<(int, int, int)>(solid.Select(s => (s.Dx, s.Dy, s.Dz)));
        var positions = new List<BlockPos>();
        var colors = new List<int>();
        int blocked = 0;
        foreach (var step in solid)
        {
            // Blocks buried inside the building would only hide the shell.
            if (!blockedOnly && Neighbours.All(n => filled.Contains((step.Dx + n.Item1, step.Dy + n.Item2, step.Dz + n.Item3)))) continue;
            var pos = StepPos(building, step);
            if (pos.Equals(building.HutPos)) continue;
            var existing = ba.GetBlock(pos);
            bool inTheWay = existing.Id != 0 && existing.Id != step.BlockId && existing.Replaceable < 6000;
            if (inTheWay) blocked++;
            if (positions.Count >= max || (blockedOnly && !inTheWay)) continue;
            positions.Add(pos);
            colors.Add(inTheWay ? OutlineBlocked : OutlineFree);
        }
        return (positions, colors, blocked, solid.Count);
    }

    /// <summary>The space a blueprint takes where the building stands, cleared air included.</summary>
    Cuboidi? BlueprintBox(Building building, Blueprint? blueprint)
    {
        if (blueprint == null || blueprint.Steps.Count == 0) return null;
        var box = new Cuboidi(int.MaxValue, int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, int.MinValue);
        foreach (var step in blueprint.Steps)
        {
            var pos = StepPos(building, step);
            box.X1 = Math.Min(box.X1, pos.X); box.Y1 = Math.Min(box.Y1, pos.Y); box.Z1 = Math.Min(box.Z1, pos.Z);
            box.X2 = Math.Max(box.X2, pos.X); box.Y2 = Math.Max(box.Y2, pos.Y); box.Z2 = Math.Max(box.Z2, pos.Z);
        }
        return box;
    }

    /// <summary>The space a building takes at a level, or just around the hut block when it has no blueprint.</summary>
    Cuboidi BoxAt(Building building, int level)
    {
        var p = building.HutPos;
        return BlueprintBox(building, level > 0 ? Blueprints.Get(building, level) : null)
            ?? new Cuboidi(p.X - 1, p.Y - 1, p.Z - 1, p.X + 1, p.Y + 2, p.Z + 1);
    }

    static bool Touch(Cuboidi a, Cuboidi b) =>
        a.X1 <= b.X2 && b.X1 <= a.X2 && a.Y1 <= b.Y2 && b.Y1 <= a.Y2 && a.Z1 <= b.Z2 && b.Z1 <= a.Z2;

    /// <summary>How many blocks apart two boxes are across the ground (0 when they meet).</summary>
    static int Gap(Cuboidi a, Cuboidi b) => Math.Max(Math.Max(0, Math.Max(b.X1 - a.X2, a.X1 - b.X2)), Math.Max(0, Math.Max(b.Z1 - a.Z2, a.Z1 - b.Z2)));

    /// <summary>
    /// The colonies' other buildings near one being placed, drawn as box outlines so the new one can go
    /// right beside them: purple for the size they have now (or are being built to), faint purple for the
    /// size of their top level when that is bigger, orange for one the new building would overlap. Also
    /// returns the names of the buildings it overlaps now, and of those it would overlap once they are
    /// upgraded. The placed building itself is left out (compared by reference).
    /// </summary>
    public (List<string> Overlaps, List<string> OverlapsLater, int Shown) NearbyOutlines(Building placing, Blueprint blueprint, List<BlockPos> positions, List<int> colors, int max)
    {
        var overlaps = new List<string>();
        var later = new List<string>();
        int range = Config.NearbyOutlineRange;
        if (range <= 0 || BlueprintBox(placing, blueprint) is not { } box) return (overlaps, later, 0);

        var near = new List<(Building Building, Cuboidi Now, Cuboidi? Top, int Gap)>();
        foreach (var colony in All)
        foreach (var other in colony.Buildings)
        {
            if (ReferenceEquals(other, placing)) continue;
            // A quick check on the hut block first; no building is wider than this.
            if (Math.Abs(other.HutPos.X - box.X1) > range + 128 || Math.Abs(other.HutPos.Z - box.Z1) > range + 128) continue;
            int topLevel = Blueprints.MaxLevel(other);
            int level = Math.Min(Math.Max(1, Math.Max(other.Level, other.TargetLevel)), Math.Max(1, topLevel));
            var now = BoxAt(other, topLevel > 0 ? level : 0);
            Cuboidi? top = topLevel > level ? BoxAt(other, topLevel) : null;
            if (top != null && top.Equals(now)) top = null;
            int gap = Math.Min(Gap(box, now), top == null ? int.MaxValue : Gap(box, top));
            if (gap <= range) near.Add((other, now, top, gap));
        }

        int shown = 0;
        foreach (var (other, now, top, _) in near.OrderBy(n => n.Gap).Take(Math.Max(1, Config.NearbyOutlineMax)))
        {
            bool overlapNow = Touch(box, now);
            bool overlapTop = !overlapNow && top != null && Touch(box, top);
            if (overlapNow) overlaps.Add(BuildingName(other));
            if (overlapTop) later.Add(BuildingName(other));
            if (top != null) AddBoxEdges(top, overlapTop ? OutlineOverlap : OutlineNearbyTop, positions, colors, max);
            AddBoxEdges(now, overlapNow ? OutlineOverlap : OutlineNearby, positions, colors, max);
            shown++;
        }
        return (overlaps, later, shown);
    }

    /// <summary>Adds the twelve edges of a box (corners included once) to the highlight, up to max blocks in all.</summary>
    static void AddBoxEdges(Cuboidi box, int color, List<BlockPos> positions, List<int> colors, int max)
    {
        for (int x = box.X1; x <= box.X2; x++)
        for (int y = box.Y1; y <= box.Y2; y++)
        for (int z = box.Z1; z <= box.Z2; z++)
        {
            // On an edge when at least two of the three coordinates are on the box's sides.
            int sides = (x == box.X1 || x == box.X2 ? 1 : 0) + (y == box.Y1 || y == box.Y2 ? 1 : 0) + (z == box.Z1 || z == box.Z2 ? 1 : 0);
            if (sides < 2) continue;
            if (positions.Count >= max) return;
            positions.Add(new BlockPos(x, y, z));
            colors.Add(color);
        }
    }
}
