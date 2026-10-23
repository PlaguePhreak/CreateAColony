using System.Collections.Generic;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// A quarrier works from a Quarry: it digs out the square pit beside the Quarry a layer at a time with a
/// pickaxe, leaving a staircase round the edge to walk down. The stone goes into its
/// inventory and from there to the Quarry's storage.
/// </summary>
public class AiTaskSettlerQuarrier : AiTaskSettlerDigger
{
    public AiTaskSettlerQuarrier(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Quarrier;
    protected override string HutType => BuildingTypes.Quarry;
    protected override float WorkSeconds => manager!.Config.QuarrySecondsPerBlock;

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        if (FetchBetterPick(citizen, storage)) return true;
        if (manager!.QuarryPit(colony, hut) == null)
        {
            SetStatus("quarry-no-room");
            return false;
        }
        var next = manager.NextQuarryBlock(colony, hut, entity.Pos.XYZ, IsUnreachable, out bool done);
        if (next == null)
        {
            SetStatus(done ? "quarry-done" : "quarry-blocked");
            return false;
        }
        SetStatus("quarrying");
        return GoWork(next);
    }

    /// <summary>The quarrier stands on the block it digs, or beside it.</summary>
    protected override Vec3d WalkGoal() => target.ToVec3d().Add(0.5, 1, 0.5);

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        // Too hard for the pick: left alone for a while, until a better pick comes.
        if (!Dig(colony, citizen, hut)) MarkUnreachable(target);
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-quarrier-dig");
}
