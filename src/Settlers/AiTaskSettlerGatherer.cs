using System.Collections.Generic;
using System.Linq;
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
/// edible mushrooms, and loose stones, flints and sticks, and brings them to the hut storage. When the colony
/// is waiting for dry grass it cuts tall grass first. Wherever it works it puts the plants it sees on the
/// colony's map. With nothing to pick it walks out now and then just to look around.
/// </summary>
public class AiTaskSettlerGatherer : AiTaskSettlerWorker
{
    /// <summary>True while walking out only to look around, with nothing to pick there.</summary>
    bool looking;
    /// <summary>True while going to cut tall grass because the colony needs dry grass.</summary>
    bool cutting;
    /// <summary>Where it last looked around on the move, and when (ms), so it marks what it passes.</summary>
    Vec3d? lastLookPos;
    long lastLookMs;

    public AiTaskSettlerGatherer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Gatherer;
    protected override string HutType => BuildingTypes.Gatherer;
    protected override EnumTool Tool => EnumTool.Knife;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => looking ? manager!.Config.ExplorerSecondsPerLook : manager!.Config.GathererSecondsPerPick;
    protected override bool NeedsTool => false;
    protected override bool WorkNeedsTool => false;

    /// <summary>It carries a knife if its hut has one, for recipes that take it (config HutRecipeTools).</summary>
    protected override bool AlsoKeeps(ItemStack stack) => IsRecipeTool(stack);

    protected override bool PlanFirst(Colony colony, CitizenRecord citizen, Building hut) =>
        FetchRecipeTools(colony, citizen, hut, manager!.HutStorage(colony, hut).Cast<BlockEntityContainer>().ToList());

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        bool lookOnly = hut.GatherMode == GatherModes.Look;
        int rank = int.MaxValue;
        var spot = lookOnly ? null : manager!.NextForage(colony, hut, entity.Pos.XYZ, IsUnreachable, out rank);
        // Dry grass the colony is waiting for is one of its requests: it goes where requests are on the collecting order.
        bool grassFirst = spot == null || ColonyManager.GatherOrder(hut).IndexOf(ColonyManager.GatherRequests) <= rank;
        if (!lookOnly && grassFirst && manager!.GrassShort(colony) > 0 && manager.NextGrass(colony, hut, entity.Pos.XYZ, IsUnreachable) is { } grass)
        {
            looking = false;
            cutting = true;
            workRange = 2.5f;
            SetStatus("cutting-grass");
            return GoWork(grass);
        }
        cutting = false;
        if (spot != null)
        {
            looking = false;
            workRange = 2.5f;
            SetStatus("gathering");
            return GoWork(spot);
        }
        spot = manager!.ForageWalk(colony, hut, IsUnreachable);
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
        if (cutting)
        {
            if (!manager!.CutGrass(colony, hut, citizen, target)) MarkUnreachable(target);
        }
        else if (!looking && !manager!.Gather(colony, hut, citizen, target)) MarkUnreachable(target);
        manager!.LookAround(colony, hut, target, LookRadius(hut));
        manager.NoteForageAround(colony, hut, target, System.Math.Max(4, manager.Config.GathererLookRadius));
    }

    /// <summary>
    /// On the move it keeps looking around: every few seconds, once it has gone a few blocks, it marks what it
    /// sees on the map, so a walk out to look (or to pick something) marks everything along the way.
    /// </summary>
    public override bool ContinueExecute(float dt)
    {
        long now = entity.World.ElapsedMilliseconds;
        if (now - lastLookMs >= LookEveryMs && (lastLookPos == null || lastLookPos.SquareDistanceTo(entity.Pos.XYZ) >= LookEveryBlocks * LookEveryBlocks))
        {
            lastLookMs = now;
            lastLookPos = entity.Pos.XYZ;
            if (Colony is { } colony && colony.Buildings.Find(b => b.BuildingId == hutId) is { } hut)
            {
                manager!.LookAround(colony, hut, entity.Pos.AsBlockPos, LookRadius(hut));
            }
        }
        return base.ContinueExecute(dt);
    }

    const long LookEveryMs = 3000;
    const int LookEveryBlocks = 5;

    /// <summary>Set to only look around, it looks farther.</summary>
    int LookRadius(Building hut) => System.Math.Max(2, manager!.Config.GathererLookRadius) * (hut.GatherMode == GatherModes.Look ? 2 : 1);

    protected override string DescribeWork() => Lang.Get(cutting ? "createacolony:activity-gatherer-grass"
        : looking ? "createacolony:activity-gatherer-look" : "createacolony:activity-gatherer-pick");
}
