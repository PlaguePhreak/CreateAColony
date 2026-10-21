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
    static readonly (int, int, int)[] Neighbours = { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };

    /// <summary>The outline blocks with their colours, and how many of them are in the way. At most max blocks.</summary>
    public (List<BlockPos> Positions, List<int> Colors, int Blocked, int Total) Outline(Building building, Blueprint blueprint, int max)
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
            if (Neighbours.All(n => filled.Contains((step.Dx + n.Item1, step.Dy + n.Item2, step.Dz + n.Item3)))) continue;
            var pos = StepPos(building, step);
            if (pos.Equals(building.HutPos)) continue;
            var existing = ba.GetBlock(pos);
            bool inTheWay = existing.Id != 0 && existing.Id != step.BlockId && existing.Replaceable < 6000;
            if (inTheWay) blocked++;
            if (positions.Count >= max) continue;
            positions.Add(pos);
            colors.Add(inTheWay ? OutlineBlocked : OutlineFree);
        }
        return (positions, colors, blocked, solid.Count);
    }
}
