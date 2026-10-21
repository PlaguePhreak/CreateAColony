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
/// A forester works from a Forester's Hut: it fells the trees around the hut, nearest first, with an
/// axe, and plants a tree seed or sapling where each one stood, so the forest grows back.
/// </summary>
public class AiTaskSettlerForester : AiTaskSettlerWorker
{
    public AiTaskSettlerForester(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Forester;
    protected override string HutType => BuildingTypes.Forester;
    protected override EnumTool Tool => EnumTool.Axe;
    protected override ItemMatch Stock => ColonyManager.IsTreeSeed;
    protected override float WorkSeconds => manager!.Config.ForesterSecondsPerTree;

    protected override bool PlanWork(Colony colony, CitizenRecord citizen, Building hut, List<BlockEntityContainer> storage)
    {
        var tree = manager!.FindTree(colony, hut, IsUnreachable);
        if (tree == null)
        {
            SetStatus(manager.StillSearching(hut, "trees") ? "searching" : "no-trees");
            return false;
        }
        SetStatus("felling");
        return GoWork(tree);
    }

    protected override void DoWork(Colony colony, CitizenRecord citizen, Building hut)
    {
        var axe = Hand;
        if (axe == null || !HoldsTool) return;
        string? wood = manager!.FellTree(colony, hut, citizen, entity, target, axe);
        SyncHand(); // an axe that broke on the last log must vanish from the model too
        if (wood != null || entity.World.BlockAccessor.GetBlock(target).Id == 0) manager.Replant(colony, citizen, target, wood);
    }

    protected override string DescribeWork() => Lang.Get("createacolony:activity-forester-fell");
}
