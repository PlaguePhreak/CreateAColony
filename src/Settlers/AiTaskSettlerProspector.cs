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
/// A prospector works from a Prospector's Hut: it walks a grid of spots around the hut and taps the
/// ground at each with a prospecting pick, taking the same density reading a player would. Miners dig
/// where the readings are best, and every colony member sees the readings on their ore map.
/// </summary>
public class AiTaskSettlerProspector : AiTaskSettlerWorker
{
    public AiTaskSettlerProspector(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Prospector;
    protected override string HutType => BuildingTypes.Prospector;
    protected override EnumTool Tool => EnumTool.Probe;
    protected override ItemMatch Stock => _ => false;
    protected override float WorkSeconds => manager!.Config.ProspectorSecondsPerReading;

    protected override bool IsToolStack(ItemStack? stack) => ColonyManager.IsProspectingPick(stack);

    protected override void RequestTool(Colony colony, Building hut) => manager!.EnsureProspectingPickRequest(colony, hut);

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var spot = manager!.NextProspectSpot(colony, hut, IsUnreachable);
        if (spot == null)
        {
            SetStatus("prospected-all");
            return false;
        }
        SetStatus("prospecting");
        return GoWork(spot);
    }

    /// <summary>The spot to stand on; a spot that was out of sight gets its real height once its land loads.</summary>
    protected override Vec3d WalkGoal()
    {
        var ba = entity.World.BlockAccessor;
        if (ba.GetChunkAtBlockPos(target) != null) target.Y = ba.GetTerrainMapheightAt(target) + 1;
        return target.ToVec3d().Add(0.5, 0, 0.5);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var pick = Hand;
        if (pick == null || !HoldsTool) return;
        WalkGoal(); // the height of a far spot is only known once there
        if (!manager!.TakeReading(colony, hut, citizen, entity, target, pick)) MarkUnreachable(target);
        SyncHand(); // a pick that wore out must vanish from the model too
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-prospector-read");
}
