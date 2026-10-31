using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CreateAColony.Colonies;

/// <summary>
/// The ground a building stands in (Aaron, 0.109.0). Builders fill any gap under a building's floor with soil down
/// to the ground, so nothing hangs over a slope. Finished buildings are looked over now and then: when ground has
/// got inside one (it was built before builders dug it out, or the land was changed) or there is a gap under it,
/// the building gets an upkeep work order, and a builder digs the ground out and fills the gaps. An upkeep order
/// touches nothing else, so furniture and blocks the players changed stay as they are.
/// </summary>
public partial class ColonyManager
{
    readonly Dictionary<int, long> upkeepCheckedMs = new();

    /// <summary>Air, or something a block can go in place of (grass, plants, snow), but not water.</summary>
    static bool IsGap(Block block) => block.Id == 0 || (block.Replaceable >= 6000 && !block.IsLiquid());

    /// <summary>Solid natural ground, which a building's empty spaces should not hold.</summary>
    static bool IsLooseGround(Block block) => block.Id != 0 && (IsSoil(block) || IsNaturalGround(block)) && !IsFarmland(block);

    /// <summary>
    /// The gaps under a building's floor to fill, the lowest of each column first, with the soil step that fills
    /// each. A column is filled down to the ground, or FoundationDepth blocks at most.
    /// </summary>
    public IEnumerable<(BuildStep Step, BlockPos Pos)> FoundationGaps(Colony colony, Building building, Blueprint blueprint)
    {
        if (Config.FoundationDepth <= 0 || blueprint.Footing.Count == 0) yield break;
        var soil = sapi.World.GetBlock(new AssetLocation("game:soil-medium-none"));
        if (soil == null) yield break;
        var ba = sapi.World.BlockAccessor;
        foreach (var (dx, dy, dz) in blueprint.Footing)
        {
            var floor = StepPos(building, new BuildStep(dx, dy, dz, 0));
            if (ba.GetChunkAtBlockPos(floor) == null) continue;
            int depth = 0;
            while (depth < Config.FoundationDepth && IsGap(ba.GetBlock(floor.DownCopy(depth + 1)))) depth++;
            for (int d = depth; d >= 1; d--)
            {
                var pos = floor.DownCopy(d);
                if (!CanWorkAt(colony, pos) || GetBuilding(colony, pos) != null) continue;
                yield return (new BuildStep(dx, dy - d, dz, soil.Id), pos);
            }
        }
    }

    /// <summary>A step an upkeep order does: ground where the building should be empty.</summary>
    bool IsUpkeepStep(BuildStep step, BlockPos pos) =>
        step.BlockId == 0 && IsLooseGround(sapi.World.BlockAccessor.GetBlock(pos));

    /// <summary>True when ground has got into the finished building, or there is a gap under its floor.</summary>
    bool NeedsUpkeep(Colony colony, Building building, Blueprint blueprint) =>
        FoundationGaps(colony, building, blueprint).Any()
        || blueprint.Steps.Any(s => IsUpkeepStep(s, StepPos(building, s)) && !IsStepDone(colony, s, StepPos(building, s)));

    /// <summary>
    /// Looks one finished building over every so often (BuildingUpkeepSeconds) and gives it an upkeep work order when
    /// ground has got into it or there is a gap under it.
    /// </summary>
    void TickUpkeep(Colony colony)
    {
        if (Config.BuildingUpkeepSeconds <= 0) return;
        long now = sapi.World.ElapsedMilliseconds;
        foreach (var building in colony.Buildings.Where(b => b.Level > 0 && !b.HasWorkOrder && !b.Removing && b.Type != BuildingTypes.MineshaftEntrance))
        {
            if (upkeepCheckedMs.TryGetValue(building.BuildingId, out long at) && now - at < Config.BuildingUpkeepSeconds * 1000L) continue;
            upkeepCheckedMs[building.BuildingId] = now;
            if (Blueprints.Get(building, building.Level) is not { Steps.Count: > 0 } blueprint) continue;
            if (sapi.World.BlockAccessor.GetChunkAtBlockPos(building.HutPos) == null) continue;
            if (!NeedsUpkeep(colony, building, blueprint)) continue;
            building.TargetLevel = building.Level;
            building.Upkeep = true;
            building.Cursor = 0;
            building.BuilderCitizenId = 0;
            BuildStatus.Remove(building.BuildingId);
            MarkDirty(colony);
            LogEvent(colony, "event-upkeep", "@building-" + building.Type);
            return; // one at a time, so the builders are not swamped
        }
    }
}
