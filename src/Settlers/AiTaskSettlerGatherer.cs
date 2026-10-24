using System.Collections.Generic;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Settlers;

/// <summary>
/// A gatherer works from a Gatherer's Hut: it forages around the hut by hand, picking ripe berries and fruit,
/// edible mushrooms, and loose stones, flints and sticks, and brings them to the hut storage. Wherever it
/// works it puts the plants it sees on the colony's map. With nothing to pick it walks out now and then just
/// to look around.
/// </summary>
public class AiTaskSettlerGatherer : AiTaskSettlerWorker
{
    /// <summary>True while walking out only to look around, with nothing to pick there.</summary>
    bool looking;

    public AiTaskSettlerGatherer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Gatherer;
    protected override string HutType => BuildingTypes.Gatherer;
    protected override EnumTool Tool => EnumTool.Knife;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => looking ? manager!.Config.ExplorerSecondsPerLook : manager!.Config.GathererSecondsPerPick;
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var spot = manager!.NextForage(colony, hut, entity.Pos.XYZ, IsUnreachable);
        if (spot != null)
        {
            looking = false;
            workRange = 2.5f;
            SetStatus("gathering");
            return GoWork(spot);
        }
        spot = manager.ForageWalk(colony, hut, IsUnreachable);
        if (spot != null)
        {
            looking = true;
            workRange = 3f;
            SetStatus("foraging-look");
            return GoWork(spot);
        }
        SetStatus("gathered-all");
        return false;
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        if (!looking && !manager!.Gather(colony, hut, citizen, target)) MarkUnreachable(target);
        manager!.LookAround(colony, hut, target, System.Math.Max(2, manager.Config.GathererLookRadius));
        manager.NoteForageAround(colony, hut, target, System.Math.Max(4, manager.Config.GathererLookRadius));
    }

    protected override string DescribeWork() => Lang.Get(looking ? "createacolony:activity-gatherer-look" : "createacolony:activity-gatherer-pick");
}
