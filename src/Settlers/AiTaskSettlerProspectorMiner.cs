using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// A prospector set to Mine ore at its Prospector's Hut: it works like a Miner's Hut's miner on a trip to
/// prospected ore, with a pickaxe, torches and dirt to fill finished mines in, but it has no shaft. It
/// goes to the best reading in the hut's work area (or of the ore chosen on the Ore tab), digs a
/// staircase down to that ore and digs it out, then moves on to the next. Each prospector keeps its own
/// trip, so two on Mine ore work different readings. Its work trains Mining.
/// </summary>
public class AiTaskSettlerProspectorMiner : AiTaskSettlerMiner
{
    public AiTaskSettlerProspectorMiner(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Prospector;
    protected override string HutType => BuildingTypes.Prospector;
    protected override bool ForRole(int role) => role == ProspectorRoles.Mine;
    protected override bool NodeSearches => false;

    protected override MineTrip TripOf(CitizenRecord citizen, Building hut) => citizen.Trip;

    protected override ColonyManager.MineJob? NextJob(Colony colony, CitizenRecord citizen, Building hut, BlockPos at, bool canFill, bool canSearch, out string status)
    {
        if (canFill && manager!.NextFillJob(colony, citizen.Trip, IsUnreachable, out status) is { } fill) return fill;
        return manager!.NextSiteJob(colony, hut, citizen.Trip, IsUnreachable, out status);
    }
}
