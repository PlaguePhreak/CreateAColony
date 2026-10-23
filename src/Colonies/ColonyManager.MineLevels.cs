using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// The Miner's Hut window's Levels, Settings, Guards and Required resources tabs, as in MineColonies' Mine.
/// A level is one loop of the entrance's stairs (level 1 at the top); sections hang off the loop their
/// tunnel starts from. Officers pick the level new sections go on, ask for a level to be put back as it
/// was (Repair), cap how deep the mine goes, and choose the block holes in the floor are filled with.
/// </summary>
public partial class ColonyManager
{
    /// <summary>The level each Miner's Hut's miner is working on now, for the Levels tab. Not saved.</summary>
    readonly Dictionary<int, int> mineshaftWorking = new();

    /// <summary>How many times the entrance's stairs repeat below the top loop: by hut level, capped by Max depth.</summary>
    public int MineshaftRepeats(Building hut)
    {
        int repeats = Math.Max(0, Config.MinerEntranceRepeatsPerHutLevel) * Math.Max(1, hut.Level);
        return hut.MaxMineLevels > 0 ? Math.Min(repeats, hut.MaxMineLevels - 1) : repeats;
    }

    /// <summary>The most levels the hut's level allows, before the Max depth setting.</summary>
    public int MineshaftLevelsAllowed(Building hut) => 1 + Math.Max(0, Config.MinerEntranceRepeatsPerHutLevel) * Math.Max(1, hut.Level);

    public int MineshaftSectionLimit(Building hut) => Math.Max(0, Config.MinerSectionsPerHutLevel) * Math.Max(1, hut.Level);

    /// <summary>The height of level 1's tunnels: the entrance's highest open side, or its anchor when it has none.</summary>
    static int TopLevelY(PlacedPiece entrance) =>
        entrance.Origin.Y + (entrance.Piece.Open.Count > 0 ? entrance.Piece.Open.Max(o => o.Y) : 0);

    /// <summary>How many levels the mineshaft has: one per loop of the entrance's stairs.</summary>
    static int LevelCount(List<PlacedPiece> pieces)
    {
        var entrance = pieces[0];
        if (entrance.Piece.Period <= 0 || entrance.Piece.Open.Count == 0) return 1;
        int top = TopLevelY(entrance);
        return entrance.Piece.Open.Select(o => (int)Math.Round((top - entrance.Origin.Y - o.Y) / (double)entrance.Piece.Period)).Distinct().Count();
    }

    /// <summary>The level (1 at the top) a height in the mineshaft belongs to.</summary>
    static int LevelAt(List<PlacedPiece> pieces, int y)
    {
        var entrance = pieces[0];
        if (entrance.Piece.Period <= 0) return 1;
        int k = (int)Math.Round((TopLevelY(entrance) - y) / (double)entrance.Piece.Period);
        return Math.Clamp(k + 1, 1, LevelCount(pieces));
    }

    /// <summary>The level a section is on: the one its open sides are level with.</summary>
    static int LevelOf(List<PlacedPiece> pieces, PlacedPiece section) =>
        LevelAt(pieces, section.Origin.Y + (section.Piece.Open.Count > 0 ? section.Piece.Open[0].Y : 0));

    /// <summary>The pieces in the order the miner works them: the entrance (the way down), then a level asked to be repaired, then the rest.</summary>
    static IEnumerable<PlacedPiece> WorkOrder(List<PlacedPiece> pieces, Building hut)
    {
        if (hut.RepairLevel <= 0) return pieces;
        return pieces.Take(1).Concat(pieces.Skip(1).OrderBy(p => LevelOf(pieces, p) == hut.RepairLevel ? 0 : 1));
    }

    /// <summary>The block holes in the floor are filled with; 0 when filling is off or the block is missing.</summary>
    int FillBlockId(Building hut) =>
        MineFillBlocks.BlockCode(hut.FillBlock) is { } code && sapi.World.GetBlock(new AssetLocation(code)) is { } block ? block.Id : 0;

    /// <summary>
    /// A piece's steps, plus a fill block under each spot it opens where the natural floor is missing (a
    /// cave, a ravine) so nobody falls through, as MineColonies' fill block does.
    /// </summary>
    IEnumerable<BuildStep> StepsWithFill(PlacedPiece piece, Dictionary<BlockPos, BuildStep> cells, int fillId)
    {
        foreach (var step in piece.Piece.Steps) yield return step;
        if (fillId == 0) yield break;
        var ba = sapi.World.BlockAccessor;
        foreach (var step in piece.Piece.Steps)
        {
            if (step.BlockId != 0) continue;
            var below = piece.At(step).DownCopy();
            if (cells.ContainsKey(below) || ba.GetChunkAtBlockPos(below) == null) continue;
            if (ba.GetBlock(below).Id != 0 || ba.GetBlock(below, BlockLayersAccess.Fluid).Id != 0) continue;
            yield return new BuildStep(step.Dx, step.Dy - 1, step.Dz, fillId);
        }
    }

    /// <summary>True when the step is as the piece wants it: dug out, or its block built.</summary>
    bool StepDone(Colony colony, PlacedPiece piece, BuildStep step)
    {
        var ba = sapi.World.BlockAccessor;
        var pos = piece.At(step);
        if (ba.GetChunkAtBlockPos(pos) == null) return false;
        return step.BlockId == 0 ? IsClear(pos) || NearLiquid(pos) : KeepsBlock(colony, ba.GetBlock(pos), step.BlockId, pos);
    }

    /// <summary>One row of the Levels tab.</summary>
    public readonly record struct MineLevelInfo(int Level, int Y, int Sections, int Left, bool Working);

    /// <summary>Each level of the mineshaft with its sections and the blocks still to dig or build there; null without mineshaft pieces.</summary>
    public List<MineLevelInfo>? MineLevels(Colony colony, Building hut)
    {
        if (MineshaftPieces(colony, hut) is not { } pieces) return null;
        int count = LevelCount(pieces);
        int top = TopLevelY(pieces[0]);
        var sections = new int[count + 1];
        var left = new int[count + 1];
        foreach (var piece in pieces)
        {
            bool entrance = piece == pieces[0];
            if (!entrance) sections[LevelOf(pieces, piece)]++;
            foreach (var step in piece.Piece.Steps)
            {
                if (StepDone(colony, piece, step)) continue;
                left[entrance ? LevelAt(pieces, piece.Origin.Y + step.Dy) : LevelOf(pieces, piece)]++;
            }
        }
        int working = mineshaftWorking.GetValueOrDefault(hut.BuildingId);
        return Enumerable.Range(1, count)
            .Select(l => new MineLevelInfo(l, top - (l - 1) * Math.Max(0, pieces[0].Piece.Period), sections[l], left[l], l == working))
            .ToList();
    }

    public void SetMineLevel(Colony colony, Building hut, int level)
    {
        hut.MineLevel = Math.Max(0, level);
        MarkDirty(colony);
    }

    public void RepairMineLevel(Colony colony, Building hut, int level)
    {
        hut.RepairLevel = Math.Max(0, level);
        MarkDirty(colony);
    }

    public void SetMaxMineLevels(Colony colony, Building hut, int levels)
    {
        hut.MaxMineLevels = levels <= 0 || levels >= MineshaftLevelsAllowed(hut) ? 0 : levels;
        MarkDirty(colony);
    }

    public string? SetFillBlock(Colony colony, Building hut, string fill)
    {
        if (!MineFillBlocks.All.Contains(fill)) return "createacolony:bad-building";
        hut.FillBlock = fill;
        MarkDirty(colony);
        return null;
    }

    /// <summary>
    /// The Required resources tab of a Miner's Hut: the piece the miner works on now (the first with
    /// anything left), how many of its spots are done, and what its blocks still to build cost. Null when
    /// there is nothing to do.
    /// </summary>
    public (string Project, int Done, int Total, List<MaterialCost> Needs)? MineshaftResources(Colony colony, Building hut)
    {
        if (MineshaftPieces(colony, hut) is not { } pieces) return null;
        var cells = MineshaftCells(pieces);
        int fillId = FillBlockId(hut);
        foreach (var piece in WorkOrder(pieces, hut))
        {
            var steps = StepsWithFill(piece, cells, fillId).ToList();
            var open = steps.Where(s => !StepDone(colony, piece, s)).ToList();
            if (open.Count == 0) continue;
            var needs = new Dictionary<string, MaterialCost>();
            foreach (var step in open.Where(s => s.BlockId != 0))
            {
                foreach (var cost in NextPlacement(colony, step, piece.At(step)).Costs)
                {
                    needs[cost.Code] = needs.TryGetValue(cost.Code, out var had) ? had with { Count = had.Count + cost.Count } : cost;
                }
            }
            string project = piece == pieces[0]
                ? Lang.Get("createacolony:mine-project-entrance")
                : Lang.Get("createacolony:mine-project-section", Lang.Get("createacolony:mine-piece-" + piece.Piece.Name), LevelOf(pieces, piece));
            return (project, steps.Count - open.Count, steps.Count, needs.Values.OrderByDescending(n => n.Count).ToList());
        }
        return null;
    }

    // ---- Guards ---------------------------------------------------------------------------

    /// <summary>Guards a Miner's Hut may take on its Guards tab: one per hut level.</summary>
    public static int MineGuardSlots(Building mine) => Math.Max(1, mine.Level);

    /// <summary>Guards of Guard Towers set to Patrol mine, who can be put on a mine's Guards tab.</summary>
    public IEnumerable<CitizenRecord> MinePatrolGuards(Colony colony) => colony.Citizens.Where(c => c.Alive && c.JobCode == Jobs.Guard
        && TowerOf(colony, c) is { Type: BuildingTypes.GuardTower, GuardTask: GuardTasks.PatrolMine });

    /// <summary>The Miner's Hut whose Guards tab took this guard, or null.</summary>
    public Building? MineGuardedBy(Colony colony, CitizenRecord guard) =>
        colony.Buildings.Find(b => b.Type == BuildingTypes.Miner && b.MineGuards.Contains(guard.CitizenId));

    /// <summary>The miner a guard on Patrol mine goes with, or null when no mine took it or the mine has no miner.</summary>
    public CitizenRecord? MinerGuardedBy(Colony colony, CitizenRecord guard) =>
        MineGuardedBy(colony, guard) is { } mine ? WorkerAt(colony, mine, Jobs.Miner) : null;

    public string? AssignMineGuard(Colony colony, Building mine, CitizenRecord guard)
    {
        if (mine.Type != BuildingTypes.Miner || !MinePatrolGuards(colony).Contains(guard)) return "createacolony:bad-citizen";
        if (mine.MineGuards.Contains(guard.CitizenId)) return null;
        CleanMineGuards(colony, mine);
        if (mine.MineGuards.Count >= MineGuardSlots(mine)) return "createacolony:mine-guards-full";
        foreach (var other in colony.Buildings) other.MineGuards.Remove(guard.CitizenId);
        mine.MineGuards.Add(guard.CitizenId);
        MarkDirty(colony);
        return null;
    }

    public void UnassignMineGuard(Colony colony, Building mine, int citizenId)
    {
        mine.MineGuards.Remove(citizenId);
        MarkDirty(colony);
    }

    /// <summary>Drops guards that died, changed job, or whose tower is no longer on Patrol mine.</summary>
    public void CleanMineGuards(Colony colony, Building mine)
    {
        var valid = MinePatrolGuards(colony).Select(c => c.CitizenId).ToHashSet();
        if (mine.MineGuards.RemoveAll(id => !valid.Contains(id)) > 0) MarkDirty(colony);
    }
}
